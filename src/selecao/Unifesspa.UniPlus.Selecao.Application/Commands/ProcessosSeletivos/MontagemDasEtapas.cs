namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Monta a coleção de etapas do processo a partir do payload, reconciliando por <c>Id</c> em vez de
/// recriá-la: a etapa cujo <c>Id</c>, ecoado pelo cliente a partir da leitura anterior, ainda existe no
/// processo é ATUALIZADA na mesma instância rastreada. Isso preserva a referência que os critérios de
/// desempate e de eliminação da classificação guardam — do contrário, todo PUT geraria etapas com
/// identidade nova e invalidaria essas referências.
/// </summary>
/// <remarks>
/// A conferência da forma, do caráter e da avaliação já passou limpa quando este passo roda; as falhas da
/// criação e da atualização são defesa em profundidade contra dessincronia entre as checagens, nunca
/// efeito do payload. Quem chama descarta as alterações rastreadas na falha, porque as etapas existentes
/// já foram mutadas.
/// </remarks>
internal sealed class MontagemDasEtapas(
    IReadOnlyDictionary<Guid, EtapaProcesso> existentes,
    TiposDasEtapasResolvidos tipos,
    AvaliacaoDasEtapasResolvida avaliacao)
{
    public Result<List<EtapaProcesso>> Montar(IReadOnlyList<EtapaProcessoInput> etapas)
    {
        List<EtapaProcesso> montadas = [];
        for (int indice = 0; indice < etapas.Count; indice++)
        {
            Result<EtapaProcesso> etapa = MontarEtapa(etapas[indice], indice);
            if (etapa.IsFailure)
            {
                return Result<List<EtapaProcesso>>.ValidationFailure(etapa.Errors);
            }

            montadas.Add(etapa.Value!);
        }

        return Result<List<EtapaProcesso>>.Success(montadas);
    }

    private Result<EtapaProcesso> MontarEtapa(EtapaProcessoInput etapa, int indice)
    {
        EtapaProcesso? existente = etapa.Id is { } id && existentes.TryGetValue(id, out EtapaProcesso? candidata) ? candidata : null;
        Result<TipoEtapaSnapshot> tipo = TipoDaEtapa(etapa, existente, indice);
        if (tipo.IsFailure)
        {
            return Result<EtapaProcesso>.Failure(tipo.Error!);
        }

        Result<EtapaProcesso> montada = existente is null
            ? EtapaProcesso.Criar(
                etapa.Nome, etapa.Carater, tipo.Value!, etapa.Peso, etapa.NotaMinima, etapa.Ordem,
                etapa.FaseCodigo, etapa.NotaMaxima, etapa.PassoDoLancamento,
                avaliacao.Formas[indice], avaliacao.Consolidacoes.GetValueOrDefault(indice),
                EtapaDoPayload.ItensDeclarados(etapa), EtapaDoPayload.NiveisDeclarados(etapa))
            : Atualizar(existente, etapa, tipo.Value!, indice);
        return montada.IsSuccess
            ? montada
            : Result<EtapaProcesso>.ValidationFailure(EtapaDoPayload.ComIndice(montada.Errors, indice));
    }

    private Result<EtapaProcesso> Atualizar(EtapaProcesso existente, EtapaProcessoInput etapa, TipoEtapaSnapshot tipo, int indice)
    {
        Result atualizada = existente.AtualizarDados(
            etapa.Nome, etapa.Carater, tipo, etapa.Peso, etapa.NotaMinima, etapa.Ordem,
            etapa.FaseCodigo, etapa.NotaMaxima, etapa.PassoDoLancamento,
            avaliacao.Formas[indice], avaliacao.Consolidacoes.GetValueOrDefault(indice),
            EtapaDoPayload.ItensDeclarados(etapa), EtapaDoPayload.NiveisDeclarados(etapa));
        return atualizada.IsSuccess
            ? Result<EtapaProcesso>.Success(existente)
            : Result<EtapaProcesso>.ValidationFailure(atualizada.Errors);
    }

    // A etapa que mantém o vínculo mantém o snapshot congelado, com os sinalizadores regravados quando o
    // caráter mudou; o vínculo novo ou alterado congela o tipo lido na conferência do caráter.
    private Result<TipoEtapaSnapshot> TipoDaEtapa(EtapaProcessoInput etapa, EtapaProcesso? existente, int indice)
    {
        if (existente is not null && existente.TipoEtapaOrigemId == etapa.TipoEtapaOrigemId)
        {
            return Result<TipoEtapaSnapshot>.Success(tipos.SinalizadoresRelidos.TryGetValue(indice, out TipoEtapaView? relido)
                ? existente.TipoEtapa.ComSinalizadores(relido.AdmitePontuacao, relido.AdmiteEliminacao)
                : existente.TipoEtapa);
        }

        TipoEtapaView tipo = tipos.Ativos[etapa.TipoEtapaOrigemId];
        return TipoEtapaSnapshot.Criar(
            tipo.Id, tipo.Codigo, tipo.Nome, tipo.AdmitePontuacao, tipo.AdmiteEliminacao, tipo.NotaDeOrigemNoEnem);
    }
}
