namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

public static class RegrasDosFatos
{
    /// <summary>
    /// O fato que decide quem recebe o acréscimo (ADR-0142). Com inscrição própria, a citação é
    /// obrigatória; citado, o fato é booleano, campo do formulário de inscrição fora de grupo ou
    /// derivado por regra do processo, e conhecido até a fase da inscrição sem depender do que só a
    /// isenção coleta — o motor o resolve para todo candidato. O ponto de resolução que o catálogo dá
    /// a cada fato entra no congelamento, por quem o lê.
    /// </summary>
    public static DomainError? ConferirFatoDeElegibilidade(
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

        return bonus.FatoElegibilidadeCodigo is not { } fato
            ? ConferirCitacaoObrigatoria(estado)
            : ConferirFatoCitado(estado, fato) ?? ConferirMomentoEmQueOFatoEConhecido(estado, fato, pontoResolucaoPorFato, membroPorAgregado);
    }

    private static DomainError? ConferirCitacaoObrigatoria(EstadoDaConfiguracao estado) =>
        estado.OrigemCandidatos == OrigemCandidatos.InscricaoPropria
            ? new DomainError(
                ProcessoSeletivoErrorCodes.FatoElegibilidadeAusente,
                "O processo com inscrição própria que aplica o acréscimo precisa citar o fato que decide quem o recebe.")
            : null;

    private static DomainError? ConferirFatoCitado(EstadoDaConfiguracao estado, string fato)
    {
        FatoColetado? coletado = estado.Itens.FirstOrDefault(f =>
            f.Finalidade == FinalidadeFormulario.Inscricao && string.Equals(f.FatoCodigo, fato, StringComparison.Ordinal));
        ConfiguracaoDerivacaoFato? derivado = estado.DerivacoesEfetivas().FirstOrDefault(d => string.Equals(d.CodigoFato, fato, StringComparison.Ordinal));
        if (coletado is null && derivado is null)
        {
            return RecusaDoFatoForaDoProcesso(estado, fato);
        }

        return coletado is { TipoRenderizacao: not TipoRenderizacao.Booleano } || derivado is { Booleano: false }
            ? new DomainError(
                ProcessoSeletivoErrorCodes.FatoElegibilidadeNaoBooleano,
                $"O fato '{fato}', que decide quem recebe o acréscimo, precisa ser booleano.")
            : null;
    }

    // O campo de grupo repetível tem um valor por membro; o acréscimo é do candidato.
    private static DomainError RecusaDoFatoForaDoProcesso(EstadoDaConfiguracao estado, string fato) =>
        estado.Campos.Any(f => f.GrupoColetadoId is not null && f.Finalidade == FinalidadeFormulario.Inscricao
                && string.Equals(f.FatoCodigo, fato, StringComparison.Ordinal))
            ? new DomainError(
                ProcessoSeletivoErrorCodes.FatoElegibilidadeDeMembroDeGrupo,
                $"O fato '{fato}', que decide quem recebe o acréscimo, é campo de membro de um grupo do formulário de inscrição; o acréscimo é decidido por um fato do candidato.")
            : new DomainError(
                ProcessoSeletivoErrorCodes.FatoElegibilidadeForaDoProcesso,
                $"O fato '{fato}', que decide quem recebe o acréscimo, não é campo do formulário de inscrição nem derivado pelo processo.");

    // Sem o formulário de inscrição na fase dele, a pendência é a do próprio formulário.
    private static DomainError? ConferirMomentoEmQueOFatoEConhecido(
        EstadoDaConfiguracao estado,
        string fato,
        IReadOnlyDictionary<string, string> pontoResolucaoPorFato,
        IReadOnlyDictionary<string, string> membroPorAgregado)
    {
        if (estado.FormularioDe(FinalidadeFormulario.Inscricao)?.FaseId is not { } faseId
            || estado.CronogramaFases.FirstOrDefault(f => f.Id == faseId) is not { } faseDaInscricao)
        {
            return null;
        }

        FaseEfetivaDoFato.FaseEfetiva efetiva = FaseEfetivaDoFato.Resolver(estado, fato, pontoResolucaoPorFato, membroPorAgregado, []);

        return efetiva.Recusa
            ?? RecusaDoFatoConhecidoDepoisDaInscricao(efetiva, faseDaInscricao, fato)
            ?? RecusaDoFatoSoDaIsencao(estado, fato, membroPorAgregado);
    }

    private static DomainError? RecusaDoFatoConhecidoDepoisDaInscricao(FaseEfetivaDoFato.FaseEfetiva efetiva, FaseCronograma faseDaInscricao, string fato) =>
        efetiva.Fase is { } fase && fase.Ordem > faseDaInscricao.Ordem
            ? new DomainError(
                ProcessoSeletivoErrorCodes.FatoElegibilidadeConhecidoDepoisDaInscricao,
                $"O fato '{fato}', que decide quem recebe o acréscimo, só é conhecido na fase '{fase.Codigo}', depois da inscrição.")
            : null;

    private static DomainError? RecusaDoFatoSoDaIsencao(EstadoDaConfiguracao estado, string fato, IReadOnlyDictionary<string, string> membroPorAgregado) =>
        FaseEfetivaDoFato.DependeDeFatoSoDaIsencao(estado, fato, membroPorAgregado, [])
            ? new DomainError(
                ProcessoSeletivoErrorCodes.FatoElegibilidadeDaIsencao,
                $"O fato '{fato}', que decide quem recebe o acréscimo, depende do que só o formulário de isenção coleta.")
            : null;
}
