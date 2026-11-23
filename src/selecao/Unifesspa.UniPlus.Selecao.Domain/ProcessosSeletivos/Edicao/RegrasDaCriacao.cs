namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os campos sem os quais um processo não nasce.</summary>
internal static class RegrasDaCriacao
{
    internal static void Conferir(
        string nome,
        TipoProcessoSnapshot tipoProcesso,
        OrigemCandidatos origemCandidatos,
        Guid unidadeAdministradoraOrigemId,
        UnidadeAdministradoraSnapshot unidadeAdministradora,
        LocalidadeRegente localidade,
        IdentificadorLegivel identificadorLegivel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ConferirNome(nome);
        ConferirTipoEOrigem(tipoProcesso, origemCandidatos);
        ConferirUnidade(unidadeAdministradoraOrigemId, unidadeAdministradora);
        ArgumentNullException.ThrowIfNull(localidade);
        ArgumentException.ThrowIfNullOrWhiteSpace(identificadorLegivel.Valor, nameof(identificadorLegivel));
    }

    private static void ConferirTipoEOrigem(TipoProcessoSnapshot tipoProcesso, OrigemCandidatos origemCandidatos)
    {
        if (tipoProcesso is null)
        {
            throw new ArgumentException("Tipo do processo é obrigatório.", nameof(tipoProcesso));
        }

        if (origemCandidatos == OrigemCandidatos.Nenhuma)
        {
            throw new ArgumentException("Origem dos candidatos é obrigatória.", nameof(origemCandidatos));
        }
    }

    private static void ConferirUnidade(Guid unidadeAdministradoraOrigemId, UnidadeAdministradoraSnapshot unidadeAdministradora)
    {
        if (unidadeAdministradoraOrigemId == Guid.Empty)
        {
            throw new ArgumentException("Unidade administradora é obrigatória.", nameof(unidadeAdministradoraOrigemId));
        }

        ArgumentNullException.ThrowIfNull(unidadeAdministradora);
    }

    private static void ConferirNome(string nome)
    {
        if (nome.Trim().Length > DocumentoDoRascunho.NomeMaxLength)
        {
            throw new ArgumentException("O nome do processo seletivo não pode passar de 300 caracteres.", nameof(nome));
        }
    }
}
