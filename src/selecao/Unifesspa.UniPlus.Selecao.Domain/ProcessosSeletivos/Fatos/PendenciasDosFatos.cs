namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class PendenciasDosFatos
{
    /// <summary>
    /// O fato que decide quem recebe o acréscimo (ADR-0142). Com inscrição própria, a citação é
    /// obrigatória; citado, o fato é booleano, campo do formulário de inscrição fora de grupo ou
    /// derivado por regra do processo, e conhecido até a fase da inscrição sem depender do que só a
    /// isenção coleta — o motor o resolve para todo candidato. O ponto de resolução que o catálogo dá
    /// a cada fato entra no congelamento, por quem o lê.
    /// </summary>
    public static DomainError? RecusaDoFatoDeElegibilidade(
        EstadoDaConfiguracao estado,
        IReadOnlyDictionary<string, string> pontoResolucaoPorFato,
        IReadOnlyDictionary<string, string> membroPorAgregado)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(pontoResolucaoPorFato);
        ArgumentNullException.ThrowIfNull(membroPorAgregado);

        if (estado.BonusRegional is not { } bonus)
        {
            return null;
        }

        if (bonus.FatoElegibilidadeCodigo is not { } fato)
        {
            return estado.OrigemCandidatos == OrigemCandidatos.InscricaoPropria
                ? new DomainError(
                    ProcessoSeletivoErrorCodes.FatoElegibilidadeAusente,
                    "O processo com inscrição própria que aplica o acréscimo precisa citar o fato que decide quem o recebe.")
                : null;
        }

        FatoColetado? coletado = estado.Itens.FirstOrDefault(f =>
            f.Finalidade == FinalidadeFormulario.Inscricao && string.Equals(f.FatoCodigo, fato, StringComparison.Ordinal));
        ConfiguracaoDerivacaoFato? derivado = estado.DerivacoesEfetivas().FirstOrDefault(d => string.Equals(d.CodigoFato, fato, StringComparison.Ordinal));
        if (coletado is null && derivado is null)
        {
            // O campo de grupo repetível tem um valor por membro; o acréscimo é do candidato.
            return estado.Campos.Any(f => f.GrupoColetadoId is not null && f.Finalidade == FinalidadeFormulario.Inscricao
                    && string.Equals(f.FatoCodigo, fato, StringComparison.Ordinal))
                ? new DomainError(
                    ProcessoSeletivoErrorCodes.FatoElegibilidadeDeMembroDeGrupo,
                    $"O fato '{fato}', que decide quem recebe o acréscimo, é campo de membro de um grupo do formulário de inscrição; o acréscimo é decidido por um fato do candidato.")
                : new DomainError(
                    ProcessoSeletivoErrorCodes.FatoElegibilidadeForaDoProcesso,
                    $"O fato '{fato}', que decide quem recebe o acréscimo, não é campo do formulário de inscrição nem derivado pelo processo.");
        }

        if (coletado is { TipoRenderizacao: not TipoRenderizacao.Booleano } || derivado is { Booleano: false })
        {
            return new DomainError(
                ProcessoSeletivoErrorCodes.FatoElegibilidadeNaoBooleano,
                $"O fato '{fato}', que decide quem recebe o acréscimo, precisa ser booleano.");
        }

        // Sem o formulário de inscrição na fase dele, a pendência é a do próprio formulário.
        if (estado.FormularioDe(FinalidadeFormulario.Inscricao)?.FaseId is not { } faseId
            || estado.CronogramaFases.FirstOrDefault(f => f.Id == faseId) is not { } faseDaInscricao)
        {
            return null;
        }

        FaseEfetivaDoFato.FaseEfetiva efetiva = FaseEfetivaDoFato.Resolver(estado, fato, pontoResolucaoPorFato, membroPorAgregado, []);
        if (efetiva.Recusa is { } recusa)
        {
            return recusa;
        }

        if (efetiva.Fase is { } fase && fase.Ordem > faseDaInscricao.Ordem)
        {
            return new DomainError(
                ProcessoSeletivoErrorCodes.FatoElegibilidadeConhecidoDepoisDaInscricao,
                $"O fato '{fato}', que decide quem recebe o acréscimo, só é conhecido na fase '{fase.Codigo}', depois da inscrição.");
        }

        return FaseEfetivaDoFato.DependeDeFatoSoDaIsencao(estado, fato, membroPorAgregado, [])
            ? new DomainError(
                ProcessoSeletivoErrorCodes.FatoElegibilidadeDaIsencao,
                $"O fato '{fato}', que decide quem recebe o acréscimo, depende do que só o formulário de isenção coleta.")
            : null;
    }

    internal static DomainError? PendenciaDoFatoDeElegibilidade(EstadoDaConfiguracao estado) => RecusaDoFatoDeElegibilidade(estado, CitacoesDosFormularios.SemCatalogo, CitacoesDosFormularios.SemCatalogo);

    /// <summary>
    /// A regra de aplicabilidade de cada modalidade ofertada, como vale na oferta, depende só de fatos
    /// que o formulário de inscrição coleta, diretamente ou pelos derivados que ela cita: a modalidade
    /// é decidida na inscrição, e o fato que ela não pergunta deixaria a modalidade sem candidato que
    /// se saiba se concorre a ela. No processo de resultado importado, a modalidade vem com o
    /// candidato, e as regras não são avaliadas.
    /// </summary>
    /// <remarks>
    /// É também a conferência das citações das derivações da oferta: a modalidade do candidato cita o
    /// que as regras citam, e a opção pelas cotas só é derivada quando uma regra a cita.
    /// </remarks>
    internal static DomainError? PendenciaDasRegrasDasModalidades(EstadoDaConfiguracao estado, IReadOnlyDictionary<string, string> membroPorAgregado)
    {
        if (estado.OrigemCandidatos != OrigemCandidatos.InscricaoPropria)
        {
            return null;
        }

        HashSet<string> daInscricao = CitacoesDosFormularios.FatosDaInscricao(estado.Itens);
        HashSet<string> camposDosGruposDaInscricao = new(
            estado.GruposColetados.Where(static g => g.Finalidade == FinalidadeFormulario.Inscricao)
                .SelectMany(static g => g.Subitens).Select(static s => s.FatoCodigo),
            StringComparer.Ordinal);
        foreach ((ModalidadeSelecionada modalidade, PredicadoDnf regra) in DerivacoesDaOferta.RegrasNaOferta(estado.DistribuicaoVagas.SelectMany(static d => d.Modalidades)))
        {
            if (regra.FatosCitados
                    .Select(f => FatoForaDaInscricao(estado, f, daInscricao, camposDosGruposDaInscricao, membroPorAgregado, new HashSet<string>(StringComparer.Ordinal)))
                    .FirstOrDefault(static f => f is not null) is { } fato)
            {
                return new DomainError(
                    ProcessoSeletivoErrorCodes.RegraDeModalidadeCitaFatoNaoColetado,
                    $"A regra de aplicabilidade da modalidade {modalidade.Codigo} depende de '{fato}', que o formulário de inscrição não pergunta.");
            }
        }

        return null;
    }

    /// <summary>
    /// O primeiro fato de que o fato depende e que o formulário de inscrição não coleta: o próprio,
    /// quando nem a inscrição o pergunta nem o processo o deriva; uma dependência do derivado; ou, no
    /// agregado sobre grupo repetível, o campo de membro que nenhum grupo da inscrição pergunta.
    /// </summary>
    internal static string? FatoForaDaInscricao(
        EstadoDaConfiguracao estado,
        string fato,
        IReadOnlySet<string> daInscricao,
        IReadOnlySet<string> camposDosGruposDaInscricao,
        IReadOnlyDictionary<string, string> membroPorAgregado,
        HashSet<string> emAvaliacao)
    {
        if (daInscricao.Contains(fato) || !emAvaliacao.Add(fato))
        {
            return null;
        }

        // O agregado se calcula sobre as ocorrências do grupo: basta que um grupo da inscrição
        // pergunte o campo de membro. Citado sozinho, o campo de grupo não decide a modalidade.
        if (membroPorAgregado.TryGetValue(fato, out string? membro))
        {
            return camposDosGruposDaInscricao.Contains(membro) ? null : membro;
        }

        bool derivado = estado.DerivacoesEfetivas().Any(d => string.Equals(d.CodigoFato, fato, StringComparison.Ordinal))
            || DerivadosDoSistema.Dependencias.ContainsKey(fato);
        return derivado
            ? FaseEfetivaDoFato.DependenciasDoFato(estado, fato, membroPorAgregado)
                .Select(d => FatoForaDaInscricao(estado, d, daInscricao, camposDosGruposDaInscricao, membroPorAgregado, emAvaliacao))
                .FirstOrDefault(static d => d is not null)
            : fato;
    }

    /// <summary>
    /// A condição de uma regra de derivação tem de citar um fato que exista no processo — coletado
    /// ou derivado (§7.3). O motor de derivação resolve os derivados em ordem, então um derivado
    /// que dependa de outro é resolúvel; o que ele não sabe fazer é resolver um fato que o processo
    /// não configura. O grafo conjunto ignora de propósito uma referência ausente (projeta só o
    /// que existe), então é esta recusa que prova a completude do vocabulário citado, antes que a
    /// aciclicidade e a ordem topológica sejam congeladas (congelamento dos parâmetros na publicação, UNI-REQ-0019).
    /// </summary>
    /// <remarks>
    /// <para>
    /// As regras dos itens e dos termos são conferidas de novo aqui, pela mesma regra da definição
    /// (<see cref="CitacoesDosFormularios.ValidarGrafoDeFatos"/>), porque os itens e as derivações mudam depois. O gatilho
    /// de exigência cita pelo mesmo vocabulário e caberá aqui quando passar a exigir os seus fatos.
    /// </para>
    /// <para>
    /// Só as derivações que o processo grava são conferidas aqui. As que a oferta supre, inclusive o
    /// egresso pela regra do sistema, são conferidas por <see cref="PendenciaDasRegrasDasModalidades"/>,
    /// que nomeia a modalidade e segue as citações até o formulário de inscrição.
    /// </para>
    /// </remarks>
    internal static DomainError? PendenciaDeFatosCitados(EstadoDaConfiguracao estado)
    {
        IReadOnlyList<ConfiguracaoDerivacaoFato> derivacoes = estado.DerivacoesEfetivas();
        HashSet<string> universo = new(
            estado.Itens.Select(static f => f.FatoCodigo).Concat(derivacoes.Select(static d => d.CodigoFato)), StringComparer.Ordinal);

        foreach (ConfiguracaoDerivacaoFato derivacao in estado.RegrasDerivacao)
        {
            foreach (RegraDerivacaoConfigurada regra in derivacao.Regras)
            {
                foreach (CondicaoRegraDerivacao condicao in regra.Condicoes)
                {
                    if (!universo.Contains(condicao.Fato))
                    {
                        return new DomainError(
                            FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado,
                            $"A regra de derivação do fato '{derivacao.CodigoFato}' cita '{condicao.Fato}', "
                            + "que este processo não coleta nem deriva.");
                    }
                }
            }
        }

        // As regras dos itens, a exibição das seções e as condições dos termos citam o que o
        // formulário conhece; os itens e as derivações podem ter mudado depois da citação.
        return CitacoesDosFormularios.CitacaoInvalidaNosFormularios(estado.Formularios, [.. estado.Itens], estado.TermosExigidos, derivacoes, estado.GruposColetados);
    }

    /// <summary>
    /// Campo opcional que alimenta derivação, negação ou impedimento, em qualquer regra viva do processo — a regra
    /// é a de <see cref="CampoQueAlimentaRegra"/>, avaliada sobre os formulários na ordem.
    /// </summary>
    internal static DomainError? PendenciaDeCampoOpcionalQueAlimentaRegra(EstadoDaConfiguracao estado) =>
        CampoQueAlimentaRegra.PrimeiroOpcional(
            estado.Itens.OrderBy(static f => f.Finalidade).ThenBy(static f => f.Ordem).Select(static f => (f.FatoCodigo, f.Obrigatoriedade.Tipo))
                .Concat(estado.GruposColetados.OrderBy(static g => g.Finalidade).ThenBy(static g => g.Ordem)
                    .SelectMany(static g => g.Subitens).Select(static f => (f.FatoCodigo, f.Obrigatoriedade.Tipo))),
            CampoQueAlimentaRegra.Fatos(
                estado.DerivacoesEfetivas().SelectMany(static c => c.FatosCitados).Concat(DependenciasDosDerivadosDoSistemaCitados(estado)),
                ReferenciasVivas.CondicoesVivasComOperador(estado).Select(static c => (c.Fato, c.Operador)),
                estado.Itens.Where(static f => f.Impedimento is not null).Select(static f => f.FatoCodigo)));

    /// <summary>Os fatos de que dependem os derivados do sistema que uma regra viva do processo cita.</summary>
    internal static IEnumerable<string> DependenciasDosDerivadosDoSistemaCitados(EstadoDaConfiguracao estado) =>
        ReferenciasVivas.CondicoesVivasComOperador(estado).Select(static c => c.Fato).Distinct(StringComparer.Ordinal)
            .SelectMany(static fato => DerivadosDoSistema.Dependencias.GetValueOrDefault(fato) ?? []);

    /// <summary>
    /// Grupo ou campo de grupo opcional que alimenta um agregado citado por regra viva do processo
    /// (<see cref="CampoQueAlimentaRegra"/>): os agregados vêm do catálogo
    /// (<paramref name="membroPorAgregado"/>), lido por quem congela o processo.
    /// </summary>
    public static DomainError? PendenciaDeGrupoQueAlimentaAgregado(EstadoDaConfiguracao estado, IReadOnlyDictionary<string, string> membroPorAgregado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        ArgumentNullException.ThrowIfNull(membroPorAgregado);

        HashSet<string> membros = new(
            ReferenciasVivas.CondicoesVivasComOperador(estado).Select(static c => c.Fato).Where(membroPorAgregado.ContainsKey).Select(a => membroPorAgregado[a]),
            StringComparer.Ordinal);
        return CampoQueAlimentaRegra.PrimeiroGrupoOpcional(
            estado.GruposColetados.OrderBy(static g => g.Finalidade).ThenBy(static g => g.Ordem)
                .Select(static g => (g.Codigo, g.Obrigatoriedade.Tipo, g.Subitens.Select(static s => (s.FatoCodigo, s.Obrigatoriedade.Tipo)))),
            membros);
    }

    /// <summary>
    /// Um fato categórico coletável cujo domínio é derivado da oferta do próprio processo
    /// (<c>CONDICAO_ATENDIMENTO</c>, <c>RECURSO_ACESSIBILIDADE</c> e o <c>TIPO_DEFICIENCIA</c> cujo campo
    /// congelou a origem do processo antes de as opções virem do cadastro) não pode ser publicado sem pelo
    /// menos um valor ofertado — obrigatório ou opcional, a pessoa candidata precisa de alguma
    /// opção para responder. Sem este gate, <c>DefinirFatosColetadosCommandHandler</c> aceita o
    /// vínculo (rascunho aceita configuração incompleta, de propósito) e
    /// <c>ResolvedorValoresSelecionaveisCongelados</c> congela a lista vazia — o formulário de
    /// inscrição publicado recebe um seletor sem nenhuma opção.
    /// </summary>
    internal static DomainError? PendenciaDeFatoColetadoSemValoresOfertados(EstadoDaConfiguracao estado)
    {
        foreach (FatoColetado fato in estado.Campos)
        {
            if (!fato.TipoRenderizacao.EhSelecao())
            {
                continue;
            }

            OrigemValoresColeta origem = RegrasDasOpcoesDeclaradas.OrigemDasOpcoes(fato);
            if (origem == OrigemValoresColeta.OpcoesDoProcesso && RegrasDasOpcoesDeclaradas.OpcoesDoProcesso(estado, fato.FatoCodigo).Count == 0)
            {
                return new DomainError(
                    "ProcessoSeletivo.FatoColetadoSemValoresOfertados",
                    $"O fato '{fato.FatoCodigo}' é coletável, mas o processo não declara nenhuma opção para ele.");
            }

            if (origem == OrigemValoresColeta.MunicipiosDoBonus && estado.BonusRegional is null)
            {
                return new DomainError(
                    "ProcessoSeletivo.FatoColetadoSemValoresOfertados",
                    $"O fato '{fato.FatoCodigo}' oferece os municípios do bônus regional, mas o processo não tem bônus regional.");
            }
        }

        return null;
    }

    /// <summary>
    /// Toda condição viva que cita um campo com os municípios do bônus regional cita município da
    /// área atual. A recusa ao redefinir o bônus só alcança os campos coletados naquele momento;
    /// conferir na publicação cobre qualquer ordem de edição, como retirar o campo da coleta,
    /// trocar a área e recolocá-lo.
    /// </summary>
    internal static DomainError? PendenciaDeMunicipioDoBonusForaDaArea(EstadoDaConfiguracao estado)
    {
        if (estado.BonusRegional is null)
        {
            return null;
        }

        HashSet<string> municipios = [.. estado.BonusRegional.Municipios.Select(static m => m.CodigoIbge)];
        FatoColetado? fato = estado.Campos.FirstOrDefault(f =>
            f.OrigemValores == OrigemValoresColeta.MunicipiosDoBonus
            && ReferenciasVivas.ReferenciaDinamicaSeriaInvalidada(estado, f.FatoCodigo, municipios));
        return fato is null
            ? null
            : new DomainError(
                "ProcessoSeletivo.MunicipioDoBonusReferenciadoPorCondicaoViva",
                $"Existe condição citando, no fato '{fato.FatoCodigo}', um município fora da área do bônus regional.");
    }

    /// <summary>
    /// O grafo de dependência conjunto (campos, fatos, exigências, seções e termos condicionados, e
    /// as quatro arestas, §6) tem
    /// de ser um DAG para ser congelável: a ordem topológica total que o snapshot congela (congelamento dos parâmetros na publicação, UNI-REQ-0019)
    /// não existe se houver ciclo. A construção do grafo (<see cref="CitacoesDosFormularios.ConstruirGrafoDependencia"/>)
    /// já detecta o ciclo — aqui ela vira gate de publicação, antes de canonicalizar.
    /// </summary>
    /// <remarks>
    /// O grafo ignora deliberadamente uma referência a fato ausente (ele projeta o que existe),
    /// então este gate <b>não</b> prova sozinho que todo fato citado existe — essa é uma recusa
    /// à parte (§7.3). Aqui só se garante a aciclicidade.
    /// </remarks>
    internal static DomainError? PendenciaDoGrafoConjunto(EstadoDaConfiguracao estado)
    {
        Result<GrafoDependenciaConjunta> grafo = CitacoesDosFormularios.ConstruirGrafoDependencia(estado);
        return grafo.IsFailure ? grafo.Error : null;
    }
}
