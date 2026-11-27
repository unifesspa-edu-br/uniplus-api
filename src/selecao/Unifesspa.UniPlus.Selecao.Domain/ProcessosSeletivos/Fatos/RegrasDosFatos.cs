namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class RegrasDosFatos
{
    /// <summary>
    /// O fato que decide quem recebe o acréscimo (ADR-0142). Com inscrição própria, a citação é
    /// obrigatória; citado, o fato é booleano, campo do formulário de inscrição fora de grupo ou
    /// derivado por regra do processo, e conhecido até a fase da inscrição sem depender do que só a
    /// isenção coleta — o sistema o resolve para todo candidato. O ponto de resolução que o catálogo dá
    /// a cada fato entra no congelamento, por quem o lê.
    /// </summary>
    public static DomainError? ConferirFatoDeElegibilidade(
        ConfiguracaoDeclarada configuracao,
        IReadOnlyDictionary<string, string> pontoResolucaoPorFato,
        IReadOnlyDictionary<string, string> membroPorAgregado)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(pontoResolucaoPorFato);
        ArgumentNullException.ThrowIfNull(membroPorAgregado);

        if (configuracao.Vagas.Fada is not { } fada)
        {
            return null;
        }

        return fada.FatoElegibilidadeCodigo is not { } fato
            ? ConferirCitacaoObrigatoria(configuracao)
            : ConferirFatoCitado(configuracao, fato) ?? ConferirMomentoEmQueOFatoEConhecido(configuracao, fato, pontoResolucaoPorFato, membroPorAgregado);
    }

    private static DomainError? ConferirCitacaoObrigatoria(ConfiguracaoDeclarada configuracao) =>
        configuracao.Identidade.OrigemCandidatos == OrigemCandidatos.InscricaoPropria
            ? new DomainError(
                ProcessoSeletivoErrorCodes.FatoElegibilidadeAusente,
                "O processo com inscrição própria que aplica o acréscimo precisa citar o fato que decide quem o recebe.")
            : null;

    private static DomainError? ConferirFatoCitado(ConfiguracaoDeclarada configuracao, string fato)
    {
        IReadOnlySet<Guid> daInscricao = configuracao.IdsDaInscricao();
        FatoColetado? coletado = configuracao.Coleta.Itens.FirstOrDefault(f =>
            f.FormularioId is { } id && daInscricao.Contains(id) && string.Equals(f.FatoCodigo, fato, StringComparison.Ordinal));
        ConfiguracaoDerivacaoFato? derivado = configuracao.DerivacoesEfetivas().FirstOrDefault(d => string.Equals(d.CodigoFato, fato, StringComparison.Ordinal));
        if (coletado is null && derivado is null)
        {
            return RecusaDoFatoForaDoProcesso(configuracao, fato);
        }

        return coletado is { TipoDeCampo: not TipoDeCampo.SimOuNao } || derivado is { Booleano: false }
            ? new DomainError(
                ProcessoSeletivoErrorCodes.FatoElegibilidadeNaoBooleano,
                "O fato que decide quem recebe o acréscimo precisa ser booleano.")
            : null;
    }

    // O campo de grupo repetível tem um valor por membro; o acréscimo é do candidato.
    private static DomainError RecusaDoFatoForaDoProcesso(ConfiguracaoDeclarada configuracao, string fato)
    {
        IReadOnlySet<Guid> daInscricao = configuracao.IdsDaInscricao();
        return configuracao.Coleta.Campos.Any(f => f.GrupoColetadoId is not null && f.FormularioId is { } id && daInscricao.Contains(id)
                && string.Equals(f.FatoCodigo, fato, StringComparison.Ordinal))
            ? new DomainError(
                ProcessoSeletivoErrorCodes.FatoElegibilidadeDeMembroDeGrupo,
                "O fato que decide quem recebe o acréscimo é campo de membro de um grupo de formulário da inscrição; o acréscimo é decidido por um fato do candidato.")
            : new DomainError(
                ProcessoSeletivoErrorCodes.FatoElegibilidadeForaDoProcesso,
                "O fato que decide quem recebe o acréscimo não é campo de formulário da inscrição nem derivado pelo processo.");
    }

    // Sem formulário da inscrição na fase dele, a pendência é a da obrigatoriedade dos formulários.
    private static DomainError? ConferirMomentoEmQueOFatoEConhecido(
        ConfiguracaoDeclarada configuracao,
        string fato,
        IReadOnlyDictionary<string, string> pontoResolucaoPorFato,
        IReadOnlyDictionary<string, string> membroPorAgregado)
    {
        OrdemDoCronograma ordem = OrdemDoCronograma.Do(configuracao.Cronograma.CronogramaFases);
        if (FaseDaInscricao(configuracao, ordem) is not { } faseDaInscricao)
        {
            return null;
        }

        FaseConhecida conhecida = FaseEmQueOFatoFicaConhecido.Resolver(
            new OrigensDoFatoNaConfiguracaoDeclarada(configuracao, pontoResolucaoPorFato, membroPorAgregado), ordem, fato);

        return conhecida.Recusa
            ?? RecusaDoFatoConhecidoDepoisDaInscricao(ordem, conhecida, faseDaInscricao)
            ?? RecusaDoFatoSoDeFormularioFacultativo(configuracao, fato, membroPorAgregado, faseDaInscricao);
    }

    // A inscrição se completa na última fase que a coleta: ali o fato precisa estar conhecido.
    private static FaseCronograma? FaseDaInscricao(ConfiguracaoDeclarada configuracao, OrdemDoCronograma ordem) =>
        configuracao.DaInscricao()
            .Select(formulario => configuracao.Cronograma.CronogramaFases.FirstOrDefault(fase => fase.Id == formulario.FaseId))
            .OfType<FaseCronograma>()
            .Aggregate((FaseCronograma?)null, (ultima, fase) => ultima is null || ordem.Posterior(fase, ultima) ? fase : ultima);

    private static DomainError? RecusaDoFatoConhecidoDepoisDaInscricao(
        OrdemDoCronograma ordem, FaseConhecida conhecida, FaseCronograma faseDaInscricao) =>
        conhecida.Fase is { } fase && ordem.Posterior(fase, faseDaInscricao)
            ? new DomainError(
                ProcessoSeletivoErrorCodes.FatoElegibilidadeConhecidoDepoisDaInscricao,
                $"O fato que decide quem recebe o acréscimo só é conhecido na fase {fase.Citacao}, depois da inscrição.")
            : null;

    private static DomainError? RecusaDoFatoSoDeFormularioFacultativo(
        ConfiguracaoDeclarada configuracao, string fato, IReadOnlyDictionary<string, string> membroPorAgregado, FaseCronograma faseDaInscricao) =>
        SoDeFormularioQueAInscricaoNaoAlcanca(configuracao, fato, membroPorAgregado, new PontoDeUso(faseDaInscricao, null), [])
            ? new DomainError(
                ProcessoSeletivoErrorCodes.FatoElegibilidadeDaIsencao,
                "O fato que decide quem recebe o acréscimo depende do que só formulário facultativo coleta.")
            : null;

    // O fato que nenhum formulário coleta não tem resposta a alcançar: vale a das dependências dele.
    private static bool SoDeFormularioQueAInscricaoNaoAlcanca(
        ConfiguracaoDeclarada configuracao, string fato, IReadOnlyDictionary<string, string> membroPorAgregado, PontoDeUso ponto, HashSet<string> emAvaliacao)
    {
        FormularioDeclarado[] produtores = [.. FormulariosQueColetam(configuracao, fato)];
        if (produtores.Length > 0)
        {
            return !produtores.Any(produtor => AlcanceDasRespostas.Alcanca(ponto, produtor, configuracao.Cronograma.CronogramaFases));
        }

        return emAvaliacao.Add(fato)
            && FaseEfetivaDoFato.DependenciasDoFato(configuracao, fato, membroPorAgregado)
                .Any(dependencia => SoDeFormularioQueAInscricaoNaoAlcanca(configuracao, dependencia, membroPorAgregado, ponto, emAvaliacao));
    }

    private static IEnumerable<FormularioDeclarado> FormulariosQueColetam(ConfiguracaoDeclarada configuracao, string fato) =>
        configuracao.Coleta.Campos
            .Where(campo => string.Equals(campo.FatoCodigo, fato, StringComparison.Ordinal))
            .Select(campo => configuracao.Coleta.Formularios.FirstOrDefault(formulario => formulario.Id == campo.FormularioId))
            .OfType<FormularioProcesso>()
            .Select(static formulario => formulario.Declarado);
}
