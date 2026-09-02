namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Avalia um conjunto de <see cref="ObrigatoriedadeLegal"/> já resolvido
/// (vigente para o tipo do processo, na data de corte) contra o estado vivo
/// de um <see cref="ProcessoSeletivo"/> (Story #853 §3.1/§3.2). Domain
/// service <b>puro</b>: recebe a lista de regras já filtrada pelo chamador
/// (Application, que tem o repositório — ADR-0042) e nunca lê relógio
/// (ADR-0068) nem I/O.
/// </summary>
/// <remarks>
/// O switch cobre as 7 variantes de <see cref="PredicadoObrigatoriedade"/>
/// explicitamente por tipo — <c>BonusObrigatorio</c>, oitava variante
/// original, foi descartada (ADR-0114, executado por esta story):
/// <c>ConfiguracaoBonusRegional</c> é global ao processo, sem lista de
/// modalidades, tornando a variante incompatível com o agregado real.
/// <b>Correção sobre o xmldoc de <see cref="PredicadoObrigatoriedade"/></b>:
/// o padrão "CS8509 sem catch-all" que o projeto usa para <c>enum</c> (ex.
/// <see cref="Unifesspa.UniPlus.Selecao.Domain.Enums.TipoDominioFatoCodigo"/>)
/// não se aplica aqui — o Roslyn não prova exaustividade de switch sobre uma
/// hierarquia de classes/records aberta (mesmo com todo derivado
/// <c>sealed</c>), só sobre o conjunto fechado de valores de um <c>enum</c>.
/// O braço final é um discard que lança <see cref="UnreachableException"/>: uma
/// 8ª variante ainda compilaria, mas falharia alto em runtime em vez de ser
/// silenciosamente ignorada.
/// </remarks>
/// <remarks>
/// Nenhum ramo compara <see cref="ProcessoSeletivo.TipoProcesso"/> nem qualquer
/// rótulo institucional — a flexibilidade entre tipos de processo vem
/// inteiramente de quais regras o cadastro tem vigentes para
/// <paramref name="tipoProcessoCodigoAvaliado"/>, nunca de um <c>if</c>
/// aqui dentro.
/// </remarks>
public static class AvaliadorConformidadeLegal
{
    /// <param name="identidades">
    /// Identidade viva de cada código de cadastro, resolvida pela camada de aplicação.
    /// Chega como dado, não como leitor: este serviço é domínio puro e não consulta
    /// cadastro. Um código ausente do mapa não designa item vivo algum — a conferência de
    /// referências já recusa a regra antes de chegar aqui.
    /// </param>
    public static ResultadoConformidade Avaliar(
        ProcessoSeletivo processo,
        string tipoProcessoCodigoAvaliado,
        IReadOnlyList<ObrigatoriedadeLegal> regras,
        IdentidadesDeCadastro identidades)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoProcessoCodigoAvaliado);
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(identidades);

        List<RegraAvaliada> avaliadas = new(regras.Count);
        List<string> avisos = [];

        foreach (ObrigatoriedadeLegal regra in regras)
        {
            (bool aprovada, string? motivo, string? aviso) = AvaliarPredicado(processo, identidades, regra.Predicado);

            avaliadas.Add(new RegraAvaliada(
                regra.Id,
                regra.RegraCodigo,
                regra.Categoria,
                tipoProcessoCodigoAvaliado,
                regra.Predicado,
                aprovada,
                motivo,
                regra.BaseLegal,
                regra.AtoNormativoUrl,
                regra.PortariaInternaCodigo,
                regra.DescricaoHumana,
                regra.VigenciaInicio,
                regra.VigenciaFim,
                regra.Hash));

            if (aviso is not null)
            {
                avisos.Add($"{regra.RegraCodigo}: {aviso}");
            }
        }

        return new ResultadoConformidade(avaliadas, avisos);
    }

    /// <summary>
    /// Devolve o veredicto, um motivo nomeado quando reprova (CA-02/CA-03/CA-09 — a razão
    /// específica, não só um booleano) e, quando aplicável, uma mensagem de aviso informativo
    /// independente da aprovação — nunca lança, mesmo para payload malformado de
    /// <see cref="Customizado"/>.
    /// </summary>
    private static (bool Aprovada, string? Motivo, string? Aviso) AvaliarPredicado(
        ProcessoSeletivo processo,
        IdentidadesDeCadastro identidades,
        PredicadoObrigatoriedade predicado) => predicado switch
        {
            EtapaObrigatoria p => AvaliarEtapaObrigatoria(processo, identidades, p),
            ModalidadesMinimas p => AvaliarModalidadesMinimas(processo, identidades, p),
            DesempateDeveIncluir p => AvaliarDesempateDeveIncluir(processo, p),
            DocumentoObrigatorioParaModalidade p => AvaliarDocumentoObrigatorioParaModalidade(processo, identidades, p),
            AtendimentoDisponivel p => AvaliarAtendimentoDisponivel(processo, identidades, p),
            ConcorrenciaDuplaObrigatoria => AvaliarConcorrenciaDuplaObrigatoria(processo),
            Customizado => (true, null, "predicado customizado — aprovado por padrão, sem verificação automática"),
            _ => throw new UnreachableException(
                $"Predicado {predicado.GetType().Name} não é uma das 7 variantes reconhecidas por este avaliador."),
        };

    /// <summary>
    /// Desfecho do confronto entre a referência que a regra cita por código e as
    /// contrapartes congeladas no processo, decidido por identidade (ADR-0129).
    /// </summary>
    private enum Casamento
    {
        /// <summary>Alguma contraparte designa o mesmo item de catálogo que a regra exige.</summary>
        Casa,

        /// <summary>
        /// Nenhuma contraparte designa o item exigido, mas alguma carrega o mesmo código
        /// apontando para outro item — o código foi reatribuído depois que a regra foi escrita.
        /// </summary>
        CodigoReatribuido,

        /// <summary>Nenhuma contraparte corresponde, nem por identidade nem por código.</summary>
        NaoCasa,
    }

    /// <summary>
    /// Confronta o código citado pela regra com as contrapartes congeladas, decidindo por
    /// identidade e reservando o código apenas para explicar o que houve.
    /// </summary>
    /// <param name="identidadeViva">Código para identificador, do cadastro vivo.</param>
    /// <param name="codigoExigido">O que a regra cita.</param>
    /// <param name="queSatisfazem">
    /// Contrapartes que, casando, aprovam a regra. Nem sempre são todas: a exigência
    /// documental só satisfaz quando cobre a modalidade incondicionalmente.
    /// </param>
    /// <param name="todasAsContrapartes">
    /// Universo consultado apenas para o diagnóstico de reatribuição — uma exigência que
    /// carrega o código mas não satisfaz ainda assim explica por que o editor a vê na tela.
    /// </param>
    /// <param name="origemDe">Identidade congelada da contraparte.</param>
    /// <param name="codigoDe">Código congelado da contraparte.</param>
    private static Casamento Casar<T>(
        IReadOnlyDictionary<string, Guid> identidadeViva,
        string? codigoExigido,
        IEnumerable<T> queSatisfazem,
        IEnumerable<T> todasAsContrapartes,
        Func<T, Guid> origemDe,
        Func<T, string> codigoDe)
    {
        // Código ausente do mapa não designa item vivo algum: não casa, e não há identidade
        // sobre a qual diagnosticar reatribuição.
        if (!identidadeViva.TryGetValue(codigoExigido ?? string.Empty, out Guid identidadeExigida))
        {
            return Casamento.NaoCasa;
        }

        if (queSatisfazem.Any(c => origemDe(c) == identidadeExigida))
        {
            return Casamento.Casa;
        }

        // Contraparte sem identidade não prova reatribuição: ela apenas não diz a que item
        // pertence, e afirmar reatribuição ali seria um fato que o dado não sustenta.
        return todasAsContrapartes.Any(c =>
            origemDe(c) != Guid.Empty
            && origemDe(c) != identidadeExigida
            && string.Equals(codigoDe(c), codigoExigido, StringComparison.Ordinal))
            ? Casamento.CodigoReatribuido
            : Casamento.NaoCasa;
    }

    private static (bool, string?, string?) AvaliarEtapaObrigatoria(
        ProcessoSeletivo processo,
        IdentidadesDeCadastro identidades,
        EtapaObrigatoria predicado)
    {
        Casamento casamento = Casar(
            identidades.TiposEtapa,
            predicado.TipoEtapaCodigo,
            processo.Etapas,
            processo.Etapas,
            static e => e.TipoEtapa.OrigemId,
            static e => e.TipoEtapa.Codigo);

        return casamento switch
        {
            Casamento.Casa => (true, null, null),
            Casamento.CodigoReatribuido => (
                false,
                $"a etapa do tipo '{predicado.TipoEtapaCodigo}' é de outro tipo — o código foi reatribuído depois que a regra foi escrita",
                null),
            _ => (false, $"etapa do tipo '{predicado.TipoEtapaCodigo}' ausente", null),
        };
    }

    /// <summary>
    /// §3.1: avalia POR OFERTA — aprova sse TODA <c>ConfiguracaoDistribuicaoVagas</c>
    /// contém todas as modalidades exigidas. Sem nenhuma oferta cadastrada, não há
    /// o que reprovar (contraprova de indistinguibilidade do processo de
    /// importação externa, Story #851 §3.4) — aprova vazio.
    /// </summary>
    private static (bool, string?, string?) AvaliarModalidadesMinimas(
        ProcessoSeletivo processo,
        IdentidadesDeCadastro identidades,
        ModalidadesMinimas predicado)
    {
        foreach (ConfiguracaoDistribuicaoVagas oferta in processo.DistribuicaoVagas)
        {
            List<string> ausentes = [];
            List<string> reatribuidos = [];

            foreach (string codigo in predicado.Codigos)
            {
                switch (Casar(
                    identidades.Modalidades,
                    codigo,
                    oferta.Modalidades,
                    oferta.Modalidades,
                    static m => m.ModalidadeOrigemId,
                    static m => m.Codigo))
                {
                    case Casamento.Casa:
                        break;
                    case Casamento.CodigoReatribuido:
                        reatribuidos.Add(codigo);
                        break;
                    default:
                        ausentes.Add(codigo);
                        break;
                }
            }

            if (reatribuidos.Count > 0)
            {
                return (
                    false,
                    $"na oferta {oferta.Id}, a(s) modalidade(s) {string.Join(", ", reatribuidos)} designa(m) outra modalidade — o código foi reatribuído depois que a regra foi escrita",
                    null);
            }

            if (ausentes.Count > 0)
            {
                return (false, $"oferta {oferta.Id} não contém a(s) modalidade(s) {string.Join(", ", ausentes)}", null);
            }
        }

        return (true, null, null);
    }

    /// <summary>
    /// Story #554 (PR #903, issue #548): gate real, substitui a reprovação conservadora que
    /// vigorou enquanto o bloco <c>documentosExigidos.exigencias</c> era stub (guarda
    /// B-01, removida junto desta task). Aprova sse existir uma <see cref="DocumentoExigido"/>
    /// do tipo pedido que cubra a modalidade INCONDICIONALMENTE.
    /// </summary>
    /// <remarks>
    /// <para>
    /// "Incondicionalmente" é a parte que faz este gate diferente do resolvedor de
    /// exigências documentais (que avalia contra um candidato REAL, com todos os fatos
    /// dele resolvidos): aqui não há candidato — só a modalidade em si. Uma exigência
    /// GERAL cobre qualquer modalidade, por definição. Uma CONDICIONAL só cobre a
    /// modalidade avaliada se o predicado DNF casar usando <b>somente</b> o fato sintético
    /// <c>MODALIDADE = predicado.Modalidade</c> — se a exigência também depender de outro
    /// fato (ex.: <c>FAIXA_ETARIA</c>), <see cref="PredicadoDnf.Avaliar"/> trata esse fato
    /// como ausente e reprova a cláusula (conservador, nunca lança): nem todo candidato da
    /// modalidade seria coberto, e é exatamente essa parcialidade que a obrigação legal —
    /// "a modalidade X DEVE exigir o documento Y", sem exceção — não admite.
    /// </para>
    /// <para>
    /// Modalidade não ofertada em nenhuma oferta do processo: nada a exigir, aprova vazio
    /// (mesmo espírito de <see cref="AvaliarModalidadesMinimas"/> sem nenhuma oferta
    /// cadastrada).
    /// </para>
    /// </remarks>
    private static (bool, string?, string?) AvaliarDocumentoObrigatorioParaModalidade(
        ProcessoSeletivo processo,
        IdentidadesDeCadastro identidades,
        DocumentoObrigatorioParaModalidade predicado)
    {
        // Sem oferta da modalidade exigida não há o que exigir. Vale também para o código
        // reatribuído: a oferta que o carrega é outra modalidade, e cobrar o documento
        // cobraria de uma modalidade que o processo não tem.
        if (!identidades.Modalidades.TryGetValue(predicado.Modalidade ?? string.Empty, out Guid identidadeDaModalidade))
        {
            return (true, null, null);
        }

        // O código congelado na oferta, não o da regra: uma exigência CONDICIONAL guarda a
        // condição com o código que valia quando foi configurada, e alimentar o fato com o
        // código novo da regra faria a cláusula não casar — reprovando exatamente o processo
        // conforme cuja renomeação esta avaliação passou a acomodar. Ofertas congeladas em
        // momentos diferentes podem carregar códigos diferentes para a mesma identidade;
        // basta uma cobrir.
        string[] codigosCongelados = [.. processo.DistribuicaoVagas
            .SelectMany(static d => d.Modalidades)
            .Where(m => m.ModalidadeOrigemId == identidadeDaModalidade)
            .Select(static m => m.Codigo)
            .Distinct(StringComparer.Ordinal)];

        if (codigosCongelados.Length == 0)
        {
            return (true, null, null);
        }

        // Achado de revisão (Story #554, PR #903): uma exigência que casa por tipo e cobre a
        // modalidade incondicionalmente, mas não DeterminaResultado() (não é obrigatória
        // nem tem consequência de indeferimento), é meramente opcional — não satisfaz a
        // obrigação legal "a modalidade X DEVE exigir o documento Y".
        // Story #916: AplicavelPara agora é ternário — só Verdadeiro (certamente aplicável)
        // prova cobertura incondicional; Indeterminado conta como "não provado", mesma
        // conclusão que Falso já dava, sem mudança de comportamento observável aqui.
        // A regra cita o tipo por código; a exigência congelou a IDENTIDADE do tipo no
        // momento em que foi configurada. Casar pelo código compararia dois retratos
        // tirados em instantes diferentes de algo que muda: renomear o tipo faria a
        // exigência legítima deixar de casar, e reciclar o código faria um documento
        // diferente passar por ele.
        //
        // Um código ausente do mapa não designa tipo vivo algum. A conferência de
        // referências recusa a regra antes de chegar aqui, e na consulta pública ela já
        // vem marcada como inavaliável — aqui a ausência apenas não casa, sem inventar
        // diagnóstico sobre uma identidade que não existe.
        if (!identidades.TiposDocumento.TryGetValue(predicado.TipoDocumento ?? string.Empty, out Guid identidadeExigida))
        {
            return (false, $"nenhuma exigência documental do tipo '{predicado.TipoDocumento}' cobre incondicionalmente a modalidade '{predicado.Modalidade}'", null);
        }

        bool cobertaIncondicionalmente = codigosCongelados.Any(codigo =>
        {
            // Só MODALIDADE entra: na publicação não há candidato, e todo outro fato é
            // legitimamente desconhecido. Ausência resolve INDETERMINADO, que aqui significa
            // "cobertura não provada" — o que se quer. Materializá-los como NAO_APLICAVEL faria
            // a cláusula colapsar em FALSO e afirmaria algo que não se sabe.
            Dictionary<string, FatoResolvido> fatoDaModalidade = new(StringComparer.Ordinal)
            {
                ["MODALIDADE"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(codigo)),
            };

            return processo.DocumentosExigidos.Any(e =>
                e.TipoDocumentoOrigemId == identidadeExigida
                && e.DeterminaResultado()
                && e.AplicavelPara(fatoDaModalidade) == Ternario.Verdadeiro);
        });

        if (cobertaIncondicionalmente)
        {
            return (true, null, null);
        }

        // Distinguir "não há exigência" de "há, mas para outro documento": a segunda só
        // acontece quando o código foi reatribuído depois que a regra foi escrita, e
        // mandar procurar uma exigência ausente faria o editor caçar o que está na tela.
        // A exigência sem identidade não prova reatribuição nenhuma: ela apenas não diz a
        // que documento pertence. Diagnosticar "o código foi reatribuído" ali seria afirmar
        // um fato que o dado não sustenta. O decoder do envelope recusa identificador
        // vazio e a factory também, então este é caminho residual — mas o diagnóstico
        // errado seria pior que o genérico.
        bool casaPeloCodigoMasNaoPelaIdentidade = processo.DocumentosExigidos.Any(e =>
            string.Equals(e.TipoDocumentoCodigo, predicado.TipoDocumento, StringComparison.Ordinal)
            && e.TipoDocumentoOrigemId != Guid.Empty
            && e.TipoDocumentoOrigemId != identidadeExigida);

        return casaPeloCodigoMasNaoPelaIdentidade
            ? (false, $"a exigência documental do tipo '{predicado.TipoDocumento}' designa outro documento — o código foi reatribuído depois que a regra foi escrita", null)
            : (false, $"nenhuma exigência documental do tipo '{predicado.TipoDocumento}' cobre incondicionalmente a modalidade '{predicado.Modalidade}'", null);
    }

    private static (bool, string?, string?) AvaliarDesempateDeveIncluir(ProcessoSeletivo processo, DesempateDeveIncluir predicado)
    {
        bool aprovada = processo.CriteriosDesempate.Any(
            c => string.Equals(c.Regra.Codigo, predicado.Criterio, StringComparison.Ordinal));
        return (aprovada, aprovada ? null : $"critério de desempate '{predicado.Criterio}' ausente", null);
    }

    private static (bool, string?, string?) AvaliarAtendimentoDisponivel(
        ProcessoSeletivo processo,
        IdentidadesDeCadastro identidades,
        AtendimentoDisponivel predicado)
    {
        if (processo.OfertaAtendimento is null)
        {
            return (false, "nenhuma oferta de atendimento especializado cadastrada", null);
        }

        IReadOnlyCollection<OfertaTipoDeficiencia> ofertados = processo.OfertaAtendimento.TiposDeficiencia;
        List<string> ausentes = [];
        List<string> reatribuidos = [];

        foreach (string necessidade in predicado.Necessidades)
        {
            switch (Casar(
                identidades.TiposDeficiencia,
                necessidade,
                ofertados,
                ofertados,
                static t => t.TipoDeficienciaOrigemId,
                static t => t.TipoDeficienciaCodigo))
            {
                case Casamento.Casa:
                    break;
                case Casamento.CodigoReatribuido:
                    reatribuidos.Add(necessidade);
                    break;
                default:
                    ausentes.Add(necessidade);
                    break;
            }
        }

        if (reatribuidos.Count > 0)
        {
            return (
                false,
                $"o atendimento ofertado para {string.Join(", ", reatribuidos)} é de outro tipo de deficiência — o código foi reatribuído depois que a regra foi escrita",
                null);
        }

        return ausentes.Count == 0
            ? (true, null, null)
            : (false, $"necessidade(s) de atendimento não ofertada(s): {string.Join(", ", ausentes)}", null);
    }

    private static (bool, string?, string?) AvaliarConcorrenciaDuplaObrigatoria(ProcessoSeletivo processo)
    {
        bool aprovada = processo.ConcorrenciaDuplaAplicavel();
        return (aprovada, aprovada ? null : "nenhuma modalidade de cota reservada (CotaReservada) cadastrada", null);
    }
}
