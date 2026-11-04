namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Monta a coleção de etapas do processo a partir das etapas conferidas, reconciliando por <c>Id</c> em vez
/// de recriá-la: a etapa cujo <c>Id</c>, ecoado pelo cliente a partir da leitura anterior, ainda existe no
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
internal static class MontagemDasEtapas
{
    public static Result<List<EtapaProcesso>> Montar(IReadOnlyList<EtapaConferida> conferidas)
    {
        List<EtapaProcesso> montadas = [];
        for (int indice = 0; indice < conferidas.Count; indice++)
        {
            Result<EtapaProcesso> etapa = MontarEtapa(conferidas[indice], indice);
            if (etapa.IsFailure)
            {
                return Result<List<EtapaProcesso>>.ValidationFailure(etapa.Errors);
            }

            montadas.Add(etapa.Value!);
        }

        return Result<List<EtapaProcesso>>.Success(montadas);
    }

    private static Result<EtapaProcesso> MontarEtapa(EtapaConferida conferida, int indice)
    {
        EtapaProcessoInput etapa = conferida.Declarada;
        Result<TipoEtapaSnapshot> tipo = TipoDaEtapa(conferida);
        if (tipo.IsFailure)
        {
            return Result<EtapaProcesso>.Failure(tipo.Error!);
        }

        Result<EtapaProcesso> montada = conferida.Existente is null
            ? EtapaProcesso.Criar(
                etapa.Nome, etapa.Carater, tipo.Value!, etapa.Peso, etapa.NotaMinima, etapa.Ordem,
                etapa.FaseCodigo, etapa.NotaMaxima, etapa.PassoDoLancamento,
                conferida.Forma, conferida.Consolidacao,
                EscritaDasEtapasDoProcesso.ItensDeclarados(etapa), EscritaDasEtapasDoProcesso.NiveisDeclarados(etapa))
            : Atualizar(conferida, tipo.Value!);
        return montada.IsSuccess
            ? montada
            : Result<EtapaProcesso>.ValidationFailure(EscritaDasEtapasDoProcesso.ComIndice(montada.Errors, indice));
    }

    private static Result<EtapaProcesso> Atualizar(EtapaConferida conferida, TipoEtapaSnapshot tipo)
    {
        EtapaProcessoInput etapa = conferida.Declarada;
        EtapaProcesso existente = conferida.Existente!;
        Result atualizada = existente.AtualizarDados(
            etapa.Nome, etapa.Carater, tipo, etapa.Peso, etapa.NotaMinima, etapa.Ordem,
            etapa.FaseCodigo, etapa.NotaMaxima, etapa.PassoDoLancamento,
            conferida.Forma, conferida.Consolidacao,
            EscritaDasEtapasDoProcesso.ItensDeclarados(etapa), EscritaDasEtapasDoProcesso.NiveisDeclarados(etapa));
        return atualizada.IsSuccess
            ? Result<EtapaProcesso>.Success(existente)
            : Result<EtapaProcesso>.ValidationFailure(atualizada.Errors);
    }

    // A etapa que mantém o vínculo mantém o snapshot congelado, com os sinalizadores regravados quando o
    // tipo foi relido porque o caráter mudou; o vínculo novo ou alterado congela o tipo lido.
    private static Result<TipoEtapaSnapshot> TipoDaEtapa(EtapaConferida conferida)
    {
        if (conferida.VinculoInalterado)
        {
            TipoEtapaSnapshot congelado = conferida.Existente!.TipoEtapa;
            return Result<TipoEtapaSnapshot>.Success(conferida.TipoLido is { } relido
                ? congelado.ComSinalizadores(relido.AdmitePontuacao, relido.AdmiteEliminacao)
                : congelado);
        }

        TipoEtapaView tipo = conferida.TipoLido!;
        return TipoEtapaSnapshot.Criar(
            tipo.Id, tipo.Codigo, tipo.Nome, tipo.AdmitePontuacao, tipo.AdmiteEliminacao, tipo.NotaDeOrigemNoEnem);
    }
}
