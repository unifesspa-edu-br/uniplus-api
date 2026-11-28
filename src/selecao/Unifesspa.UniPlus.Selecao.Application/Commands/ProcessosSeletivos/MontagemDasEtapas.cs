namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ProcessosSeletivos.Chaves;
using Domain.ValueObjects;

using Kernel.Results;

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
        ChavesEmUso chaves = ReservarChaves(conferidas);
        List<EtapaProcesso> montadas = [];
        for (int indice = 0; indice < conferidas.Count; indice++)
        {
            Result<EtapaProcesso> etapa = MontarEtapa(conferidas[indice], indice, chaves);
            if (etapa.IsFailure)
            {
                return Result<List<EtapaProcesso>>.ValidationFailure(etapa.Errors);
            }

            montadas.Add(etapa.Value!);
        }

        return Result<List<EtapaProcesso>>.Success(montadas);
    }

    private static ChavesEmUso ReservarChaves(IReadOnlyList<EtapaConferida> conferidas)
    {
        ChavesEmUso chaves = new();
        foreach (EtapaConferida conferida in conferidas.Where(static etapa => etapa.Existente is not null))
        {
            chaves.Adicionar(conferida.Existente!.Chave);
        }

        return chaves;
    }

    private static Result<EtapaProcesso> MontarEtapa(EtapaConferida conferida, int indice, ChavesEmUso chaves)
    {
        Result<TipoEtapaSnapshot> tipo = TipoDaEtapa(conferida);
        if (tipo.IsFailure)
        {
            return Result<EtapaProcesso>.Failure(tipo.Error!);
        }

        Result<EtapaProcesso> montada = conferida.Existente is null ? Criar(conferida, tipo.Value!, chaves) : Atualizar(conferida, tipo.Value!);
        return montada.IsSuccess
            ? montada
            : Result<EtapaProcesso>.ValidationFailure(EscritaDasEtapasDoProcesso.ComIndice(montada.Errors, indice));
    }

    private static Result<EtapaProcesso> Criar(EtapaConferida conferida, TipoEtapaSnapshot tipo, ChavesEmUso chaves)
    {
        ChaveLocal chave = FormacaoDeChaveLocal.Formar("etapa", tipo.Codigo, chaves);
        chaves.Adicionar(chave);
        return EtapaProcesso.Criar(
            chave, IdentificacaoDeclarada(conferida, tipo),
            AvaliacaoDeclarada(conferida), EscritaDasEtapasDoProcesso.ItensDeclarados(conferida.Declarada));
    }

    private static Result<EtapaProcesso> Atualizar(EtapaConferida conferida, TipoEtapaSnapshot tipo)
    {
        EtapaProcesso existente = conferida.Existente!;
        Result atualizada = existente.AtualizarDados(
            IdentificacaoDeclarada(conferida, tipo),
            AvaliacaoDeclarada(conferida),
            EscritaDasEtapasDoProcesso.ItensDeclarados(conferida.Declarada));
        return atualizada.IsSuccess
            ? Result<EtapaProcesso>.Success(existente)
            : Result<EtapaProcesso>.ValidationFailure(atualizada.Errors);
    }

    // O caráter já foi conferido pela forma da etapa, antes de qualquer leitura.
    private static IdentificacaoDaEtapa IdentificacaoDeclarada(EtapaConferida conferida, TipoEtapaSnapshot tipo) =>
        new(conferida.Declarada.Nome!, conferida.Carater!.Value, tipo, conferida.Declarada.Ordem, conferida.Declarada.FaseCodigo);

    private static DeclaracaoDaAvaliacaoDaEtapa AvaliacaoDeclarada(EtapaConferida conferida) =>
        EscritaDasEtapasDoProcesso.DeclaracaoDaAvaliacao(conferida.Declarada, conferida.Forma, conferida.Consolidacao);

    // A etapa que mantém o vínculo mantém o snapshot congelado, com os sinalizadores regravados quando o
    // tipo foi relido porque o caráter mudou; o vínculo novo ou alterado congela o tipo lido.
    private static Result<TipoEtapaSnapshot> TipoDaEtapa(EtapaConferida conferida)
    {
        if (conferida.VinculoInalterado)
        {
            TipoEtapaSnapshot congelado = conferida.Existente!.TipoEtapa;
            return Result<TipoEtapaSnapshot>.Success(conferida.TipoLido is { } relido
                ? congelado.ComSinalizadores(relido.Avaliacao.AdmitePontuacao, relido.Avaliacao.AdmiteEliminacao, relido.Avaliacao.ExigeCorrecaoIndependente)
                : congelado);
        }

        return CopiaDoTipoDeEtapa.De(conferida.TipoLido!);
    }
}
