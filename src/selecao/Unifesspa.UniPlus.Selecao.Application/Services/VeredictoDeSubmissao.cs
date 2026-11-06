namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O veredito de submissão: diferente da validação estrutural do rascunho
/// (<c>SalvarInscricaoCommandHandler</c>), aqui a ausência de resposta a um item visível e
/// obrigatório, um impedimento verdadeiro, um grupo fora do mínimo/máximo ou sem o candidato, e um
/// termo obrigatório não aceito (ou aceito com versão/hash diferente da versão fixada) TODOS
/// recusam a submissão — nada disso é tolerado no rascunho.
/// </summary>
/// <remarks>
/// Recebe a avaliação já pronta: a da definição inteira da versão fixada, feita pela
/// <see cref="AvaliacaoDaInscricao"/> com as etapas da inscrição concluídas, e restrita à finalidade
/// Inscrição por <see cref="AvaliacaoDaInscricao.DaInscricao"/>. Quem chama passa só essa parte: um
/// item ou termo obrigatório de outra finalidade, como a isenção, não é pendência da inscrição. Com as
/// etapas concluídas, a submissão é o candidato declarando que terminou: um campo opcional sem
/// resposta deixa de ser pendência (<see cref="EstadoFato.Indeterminado"/>) e passa a
/// <see cref="EstadoFato.NaoInformado"/>, que não pesa; só o obrigatório sem resposta permanece
/// indeterminado.
/// </remarks>
public static class VeredictoDeSubmissao
{
    /// <param name="diaDoEnvio">O dia do envio com que a avaliação foi feita, de que a recusa da faixa de data diz os limites.</param>
    public static IReadOnlyList<FieldError> Avaliar(
        AvaliacaoFormulario avaliacao,
        IReadOnlyList<TermoExigidoFormulario> termosExigidos,
        IReadOnlyList<AceiteDeTermo> aceites,
        DateOnly diaDoEnvio) =>
        [.. AvaliarDistinguindoOGeo(avaliacao, termosExigidos, aceites, diaDoEnvio, geo: null).Select(static r => r.Recusa)];

    /// <summary>
    /// O mesmo veredito, dizendo de cada recusa se ela só existe porque o Geo fora do ar deixou um derivado de residência
    /// indeterminado: com o Geo de volta, a recusa pode deixar de valer, e por isso não orienta o candidato.
    /// </summary>
    /// <param name="geo">O que o Geo deixou indeterminado; nulo quando o Geo respondeu, e então nenhuma recusa depende dele.</param>
    public static IReadOnlyList<RecusaDaSubmissao> AvaliarDistinguindoOGeo(
        AvaliacaoFormulario avaliacao,
        IReadOnlyList<TermoExigidoFormulario> termosExigidos,
        IReadOnlyList<AceiteDeTermo> aceites,
        DateOnly diaDoEnvio,
        DependenciaDoGeo? geo)
    {
        ArgumentNullException.ThrowIfNull(avaliacao);
        ArgumentNullException.ThrowIfNull(termosExigidos);
        ArgumentNullException.ThrowIfNull(aceites);

        ClassificadorDoGeo classificador = new(geo);
        List<RecusaDaSubmissao> recusas = [.. ProblemasDosItens(avaliacao.Itens, avaliacao.Fatos, caminho: null, diaDoEnvio, classificador)];

        foreach (AvaliacaoGrupo grupo in avaliacao.Grupos)
        {
            if (grupo.Visivel != Ternario.Verdadeiro)
            {
                continue;
            }

            if (!grupo.ContagemValida)
            {
                recusas.Add(new RecusaDaSubmissao(
                    Recusa(grupo.Codigo, InscricaoErrorCodes.GrupoContagemInvalida, "O número de ocorrências do grupo não respeita o mínimo ou o máximo exigido."),
                    classificador.ContagemAbaixoDoMinimo(grupo)));
            }

            if (!grupo.OcorrenciaDoCandidatoValida)
            {
                recusas.Add(Independente(Recusa(grupo.Codigo, InscricaoErrorCodes.GrupoCandidatoAusente, "O grupo inclui o candidato, mas nenhuma ocorrência o representa.")));
            }

            if (grupo.Obrigatorio == Ternario.Verdadeiro && grupo.Estado == EstadoFato.Indeterminado)
            {
                recusas.Add(Independente(Recusa(grupo.Codigo, InscricaoErrorCodes.ItemObrigatorioAusente, $"O grupo '{grupo.Codigo}' é obrigatório e não tem ocorrência nenhuma.")));
            }

            HashSet<string> idsVistos = new(StringComparer.Ordinal);
            foreach (AvaliacaoOcorrencia ocorrencia in grupo.Ocorrencias)
            {
                if (!idsVistos.Add(ocorrencia.Id))
                {
                    recusas.Add(Independente(Recusa($"{grupo.Codigo}.{ocorrencia.Id}", InscricaoErrorCodes.GrupoIdDuplicado, $"Duas ocorrências do grupo '{grupo.Codigo}' repetem o id '{ocorrencia.Id}'.")));
                }

                recusas.AddRange(ProblemasDosItens(ocorrencia.Itens, ocorrencia.Fatos, caminho: $"{grupo.Codigo}.{ocorrencia.Id}", diaDoEnvio, classificador));
            }
        }

        foreach (AvaliacaoTermo termo in avaliacao.Termos)
        {
            if (termo.Visivel != Ternario.Verdadeiro || termo.Obrigatorio != Ternario.Verdadeiro)
            {
                continue;
            }

            // avaliacao.Termos usa o código PREFIXADO pela finalidade (DefinicaoDoProcesso.CodigoDoTermo) —
            // o mesmo que a apresentação do formulário já expõe (ProjecaoDoFormularioRenderizavel.Termo).
            // termosExigidos e aceites usam o código NU, que é o que o candidato conhece e envia; a
            // recusa é sobre o aceite, então sai nele.
            TermoExigidoFormulario? exigido = termosExigidos.FirstOrDefault(t =>
                string.Equals(DefinicaoDoProcesso.CodigoDoTermo(t.Finalidade, t.Codigo), termo.Codigo, StringComparison.Ordinal));
            AceiteDeTermo? aceite = exigido is null
                ? null
                : aceites.FirstOrDefault(a => string.Equals(a.Codigo, exigido.Codigo, StringComparison.Ordinal));

            bool aceitoNaVersaoFixada = exigido is not null && aceite is not null
                && aceite.VersaoId == exigido.VersaoId
                && string.Equals(aceite.HashVersao, exigido.HashVersao, StringComparison.Ordinal);

            if (!aceitoNaVersaoFixada)
            {
                string campo = exigido?.Codigo ?? termo.Codigo;
                recusas.Add(Independente(Recusa(campo, InscricaoErrorCodes.TermoNaoAceito, $"O termo '{campo}' é obrigatório e não foi aceito na versão fixada da inscrição.")));
            }
        }

        return recusas;
    }

    /// <summary>
    /// A <see cref="AvaliacaoDaInscricao"/> descarta em silêncio, via <c>RespostaDeCampo.DentroDaOferta</c>,
    /// a resposta de um fato ofertado cujo código não pertence à oferta vigente — o item some da
    /// avaliação como se não houvesse resposta, e a submissão recusaria no máximo por obrigatoriedade
    /// (nunca por fato de o código ser inválido, e nem recusaria nada se o item for opcional). A
    /// submissão precisa da recusa explícita: por isso esta checagem roda sobre as respostas BRUTAS,
    /// antes de qualquer filtro, e sempre junto de <see cref="Avaliar"/> — nunca no lugar dele.
    /// </summary>
    public static IReadOnlyList<FieldError> RecusasDeOpcaoForaDaOferta(
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        IReadOnlyDictionary<string, IReadOnlySet<string>> ofertas)
    {
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(ofertas);

        List<FieldError> recusas = [.. ForaDaOferta(respostas, ofertas, caminho: null)];
        foreach ((string codigoGrupo, IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias) in grupos)
        {
            foreach (OcorrenciaDeGrupoInscricao ocorrencia in ocorrencias)
            {
                recusas.AddRange(ForaDaOferta(ocorrencia.Respostas, ofertas, caminho: $"{codigoGrupo}.{ocorrencia.Id}"));
            }
        }

        return recusas;
    }

    private static IEnumerable<FieldError> ForaDaOferta(
        IReadOnlyDictionary<string, JsonElement> respostas, IReadOnlyDictionary<string, IReadOnlySet<string>> ofertas, string? caminho)
    {
        foreach ((string fatoCodigo, JsonElement valor) in respostas)
        {
            if (!ofertas.TryGetValue(fatoCodigo, out IReadOnlySet<string>? oferta)
                || RespostaDeCampo.EstaVazia(valor)
                || RespostaDeCampo.Codigos(ValorDeMunicipio.ComoCodigo(valor)) is not { } codigos
                || codigos.All(oferta.Contains))
            {
                continue;
            }

            yield return Recusa(
                caminho is null ? fatoCodigo : $"{caminho}.{fatoCodigo}",
                InscricaoErrorCodes.RespostaInvalida,
                $"A resposta de '{fatoCodigo}' tem uma opção fora da oferta vigente.");
        }
    }

    private static IEnumerable<RecusaDaSubmissao> ProblemasDosItens(
        IReadOnlyList<AvaliacaoItem> itens, IReadOnlyDictionary<string, FatoResolvido> fatos, string? caminho, DateOnly diaDoEnvio, ClassificadorDoGeo classificador)
    {
        foreach (AvaliacaoItem item in itens)
        {
            if (item.Visivel != Ternario.Verdadeiro)
            {
                continue;
            }

            string campo = caminho is null ? item.FatoCodigo : $"{caminho}.{item.FatoCodigo}";

            if (item.Impedido == Ternario.Verdadeiro)
            {
                yield return Independente(Recusa(campo, InscricaoErrorCodes.Impedimento, $"A resposta de '{item.FatoCodigo}' impede a submissão desta inscrição."));
            }

            if (item.RestricoesVioladas.Count > 0)
            {
                yield return new RecusaDaSubmissao(RecusaDaResposta.De(campo, item, diaDoEnvio), classificador.RestricaoViolada(item));
            }

            if (fatos.TryGetValue(item.FatoCodigo, out FatoResolvido? resolvido) && resolvido.Estado == EstadoFato.Indeterminado)
            {
                yield return new RecusaDaSubmissao(
                    Recusa(campo, InscricaoErrorCodes.ItemObrigatorioAusente, $"O item '{item.FatoCodigo}' é obrigatório e está sem resposta."),
                    classificador.ItemObrigatorioAusente(item, campo));
            }
        }
    }

    private static RecusaDaSubmissao Independente(FieldError recusa) => new(recusa, DependeDoGeo: false);

    /// <summary>
    /// Diz quais recusas dependem do Geo: só as que a falta dele pode ter causado, por deixar indeterminado um derivado de
    /// residência de que a regra depende. Sem a dependência do Geo, nenhuma recusa depende dele.
    /// </summary>
    private sealed class ClassificadorDoGeo(DependenciaDoGeo? geo)
    {
        private readonly Dictionary<string, DefinicaoItem> _itens = geo is null
            ? []
            : geo.Definicao.Etapas
                .SelectMany(static e => e.Itens.Concat(e.Grupos.SelectMany(static g => g.Subitens)))
                .ToDictionary(static i => i.FatoCodigo, StringComparer.Ordinal);

        private readonly Dictionary<string, DefinicaoGrupo> _grupos = geo is null
            ? []
            : geo.Definicao.Etapas.SelectMany(static e => e.Grupos).ToDictionary(static g => g.Codigo, StringComparer.Ordinal);

        /// <summary>
        /// A obrigatoriedade saiu indeterminada, ou o campo foi respondido e uma restrição dele cita o derivado indeterminado, o
        /// que o deixou sem resposta que valha. O campo obrigatório em definitivo e sem resposta não depende do Geo.
        /// </summary>
        public bool ItemObrigatorioAusente(AvaliacaoItem item, string campo) =>
            geo is not null
            && (item.Obrigatorio == Ternario.Indeterminado
                || (geo.CamposRespondidos.Contains(campo)
                    && _itens.TryGetValue(item.FatoCodigo, out DefinicaoItem? definicao)
                    && definicao.Restricoes.Any(CitaDerivadoIndeterminado)));

        public bool RestricaoViolada(AvaliacaoItem item) => geo is not null && item.RestricoesVioladas.Any(CitaDerivadoIndeterminado);

        /// <summary>A contagem abaixo do mínimo, em grupo cuja obrigatoriedade saiu indeterminada; a acima do máximo não depende do Geo.</summary>
        public bool ContagemAbaixoDoMinimo(AvaliacaoGrupo grupo) =>
            geo is not null
            && grupo.Obrigatorio == Ternario.Indeterminado
            && _grupos.TryGetValue(grupo.Codigo, out DefinicaoGrupo? definicao)
            && grupo.Ocorrencias.Count < definicao.Minimo;

        private bool CitaDerivadoIndeterminado(RestricaoValor restricao) =>
            restricao.FatosCitados.Any(geo!.DerivadosIndeterminados.Contains);
    }

    private static FieldError Recusa(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));
}

/// <summary>Uma recusa do veredito e se ela só existe por causa do Geo fora do ar (<see cref="DependenciaDoGeo"/>).</summary>
public readonly record struct RecusaDaSubmissao(FieldError Recusa, bool DependeDoGeo);

/// <summary>
/// O que o Geo fora do ar deixou indeterminado na avaliação: a definição do formulário, para saber o que cada regra cita, e os
/// derivados de residência que ficaram indeterminados por causa dele e os campos que o candidato respondeu, pelo caminho
/// (<c>FATO</c> ou <c>GRUPO.ocorrencia.FATO</c>).
/// </summary>
public sealed record DependenciaDoGeo(DefinicaoFormulario Definicao, IReadOnlySet<string> DerivadosIndeterminados, IReadOnlySet<string> CamposRespondidos)
{
    /// <summary>
    /// A avaliação sem as modalidades que o Geo deixou sem decidir: a que falta responder e deriva de um derivado de residência
    /// indeterminado pode deixar de faltar com o Geo de volta, e por isso não gera recusa. A que não concorre a nenhuma
    /// modalidade está decidida, e a que falta responder por outro motivo também: essas seguem para as recusas.
    /// </summary>
    public ResultadoDaAvaliacaoDaInscricao SemAsModalidadesDoGeo(
        ResultadoDaAvaliacaoDaInscricao avaliacao, ConfiguracaoAvaliavel daPrimeiraOpcao, ConfiguracaoAvaliavel? daSegundaOpcao)
    {
        ArgumentNullException.ThrowIfNull(avaliacao);
        ArgumentNullException.ThrowIfNull(daPrimeiraOpcao);

        ModalidadesDeConcorrencia SemSeDependeDoGeo(ModalidadesDeConcorrencia modalidades, ConfiguracaoAvaliavel configuracao) =>
            modalidades.Situacao == SituacaoDasModalidades.FaltaResponder
            && AvaliacaoDaInscricao.DerivadosDoSistemaDaModalidade(configuracao).Overlaps(DerivadosIndeterminados)
                ? ModalidadesDeConcorrencia.SemDerivacao
                : modalidades;

        return avaliacao with
        {
            Modalidades = SemSeDependeDoGeo(avaliacao.Modalidades, daPrimeiraOpcao),
            ModalidadesSegundaOpcao = avaliacao.ModalidadesSegundaOpcao is { } daSegunda && daSegundaOpcao is not null
                ? SemSeDependeDoGeo(daSegunda, daSegundaOpcao)
                : avaliacao.ModalidadesSegundaOpcao,
        };
    }
}
