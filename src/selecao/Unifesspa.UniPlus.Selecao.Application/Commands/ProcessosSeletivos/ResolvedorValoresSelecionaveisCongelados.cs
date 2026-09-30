namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Resolve, para cada <see cref="FatoColetado"/> do processo, os valores que o candidato pode
/// escolher (UNI-REQ-0072) — a chave que falta hoje no envelope público: <c>fatosColetados[]</c>
/// carrega rótulo/tipo de renderização/obrigatoriedade (Story #559), mas nada sobre QUAIS
/// valores o seletor oferece.
/// </summary>
/// <remarks>
/// <para>
/// Uma entrada por <see cref="FatoColetado"/>, sempre — não só pelos de seleção. Para
/// <c>BOOLEANO</c>/<c>NUMERO</c> o valor é <see langword="null"/> (a bicondicional de
/// <c>SerializarFatosColetados</c>); para <c>SELECAO_UNICA</c>/<c>SELECAO_MULTIPLA</c> é a lista
/// — possivelmente vazia, nunca ausente. É este dicionário completo, e não um parcial só com os
/// fatos de seleção, que o decoder reproduz ao reidratar (D5), então o encoder trata os dois
/// caminhos (publicação nova e recanonicalização pós-restauração) pela MESMA forma.
/// </para>
/// <para>
/// Três origens para o valor de um fato categórico coletado (UNI-REQ-0072 §2, ADR-0136). O
/// catálogo de fonte GLOBAL (COR_RACA, SEXO, NACIONALIDADE — <c>ValoresDominioDeclarados</c>) é
/// usado tal qual, SEM filtrar por <c>Ativo</c>: é <see cref="ConferenciaDeValoresDeDominioAtivos"/>,
/// rodado ANTES deste resolvedor no mesmo congelamento, que garante que só chega aqui um fato
/// coletado cujo vocabulário inteiro está ativo. Para as demais, decide a origem copiada para o
/// campo coletado (<c>ProcessoSeletivo.OrigemDasOpcoes</c>), a mesma que o agregado confere na
/// publicação: as opções do PRÓPRIO processo, pela mesma leitura que valida predicado
/// (<c>ProcessoSeletivo.OpcoesDoProcesso</c>); ou os municípios do bônus regional, com o código
/// IBGE como valor, "Município/UF" como rótulo e a ordem alfabética de nome de
/// <c>VocabularioDeFatos.MunicipiosDoBonus</c>.
/// </para>
/// <para>
/// Não faz I/O próprio — recebe o catálogo já lido UMA vez pelo handler (D4-bis), o mesmo
/// compartilhado com o gate de valor inativo, a conferência de coletabilidade e o resolvedor de
/// metadado de fato.
/// </para>
/// </remarks>
internal static class ResolvedorValoresSelecionaveisCongelados
{

    public static Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> Resolver(
        ProcessoSeletivo processo,
        IReadOnlyDictionary<string, FatoCandidatoView> catalogo)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(catalogo);

        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresPorFato = new(StringComparer.Ordinal);
        foreach (FatoColetado fato in processo.FatosColetados)
        {
            bool ehFatoDeSelecao = fato.TipoRenderizacao is TipoRenderizacao.SelecaoUnica or TipoRenderizacao.SelecaoMultipla;
            if (!ehFatoDeSelecao)
            {
                valoresPorFato[fato.FatoCodigo] = null;
                continue;
            }

            if (!catalogo.TryGetValue(fato.FatoCodigo, out FatoCandidatoView? fatoNoCatalogo))
            {
                // Defesa em profundidade — inalcançável em uso normal: ConferenciaDeColetabilidadeDeFatos
                // já recusou, no mesmo congelamento e sobre o MESMO catálogo, um fato coletado que não
                // resolve. Chegar aqui sem resolver seria os dois passos operando sobre catálogos
                // diferentes — o que D4-bis existe para impedir.
                throw new InvalidOperationException(
                    $"O fato coletado '{fato.FatoCodigo}' não resolve no catálogo — a conferência de " +
                    "coletabilidade deveria tê-lo recusado antes deste ponto.");
            }

            Result<IReadOnlyList<ValorDominioDeclaradoCongelado>> valoresDoFato =
                ResolverValoresDoFato(fato, fatoNoCatalogo, processo);
            if (valoresDoFato.IsFailure)
            {
                return Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>>.Failure(
                    valoresDoFato.Error!);
            }

            valoresPorFato[fato.FatoCodigo] = valoresDoFato.Value;
        }

        return Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>>.Success(valoresPorFato);
    }

    /// <summary>
    /// Não é defesa em profundidade: um fato categórico DECLARADO de cardinalidade escalar/
    /// multivalorada, sem <c>ValoresDominioDeclarados</c> no catálogo e com código diferente dos
    /// dois categóricos de escopo-processo hoje conhecidos, é alcançável por dado — um fato novo
    /// no catálogo (migration futura), vinculado como <c>SELECAO_UNICA</c>/<c>SELECAO_MULTIPLA</c>
    /// porque <c>CoerenciaDeRenderizacao</c> só confere domínio/cardinalidade, não a origem dos
    /// valores. Nem a conferência de coletabilidade (o fato existe e é coletável) nem o gate de
    /// valor inativo (não tem valores declarados para inativar) recusam esse caso — por isso ele
    /// devolve <see cref="DomainError"/>, não lança.
    /// </summary>
    private static Result<IReadOnlyList<ValorDominioDeclaradoCongelado>> ResolverValoresDoFato(
        FatoColetado fato, FatoCandidatoView fatoNoCatalogo, ProcessoSeletivo processo)
    {
        string fatoCodigo = fato.FatoCodigo;
        Result<IReadOnlyList<ValorDominioDeclaradoCongelado>> resolvido = ResolverOrigemDosValores(fato, fatoNoCatalogo, processo);
        if (resolvido.IsFailure)
        {
            return resolvido;
        }

        // Defesa em profundidade (issue #1077): o caminho esperado para um fato de escopo-processo
        // (CONDICAO_ATENDIMENTO/TIPO_DEFICIENCIA) sem nenhum valor ofertado é
        // ProcessoSeletivo.PendenciaDeFatoColetadoSemValoresOfertados, avaliado ANTES deste
        // resolvedor rodar (PendenciaPreCanonicalizacao precede a canonicalização, ADR-0109 D5).
        // Chegar aqui com lista vazia significaria o gate e este resolvedor operando sobre
        // estados diferentes — o resolvedor nunca pode devolver sucesso com lista vazia, mesmo
        // que um chamador futuro esqueça de invocar o gate.
        if (resolvido.Value!.Count == 0)
        {
            return Result<IReadOnlyList<ValorDominioDeclaradoCongelado>>.Failure(new DomainError(
                "ProcessoSeletivo.FatoColetadoSemValoresOfertados",
                $"O fato coletado '{fatoCodigo}' é de seleção, mas resolveu zero valores selecionáveis."));
        }

        return resolvido;
    }

    private static Result<IReadOnlyList<ValorDominioDeclaradoCongelado>> ResolverOrigemDosValores(
        FatoColetado fato, FatoCandidatoView fatoNoCatalogo, ProcessoSeletivo processo)
    {
        string fatoCodigo = fato.FatoCodigo;

        // A origem é a copiada para o campo quando a coleta foi definida, a mesma que o agregado
        // confere na publicação — nunca a do catálogo lido agora. O catálogo só responde pelo
        // campo cuja origem é ele.
        switch (ProcessoSeletivo.OrigemDasOpcoes(fato))
        {
            case OrigemValoresColeta.OpcoesDoProcesso:
                return Result<IReadOnlyList<ValorDominioDeclaradoCongelado>>.Success([.. processo.OpcoesDoProcesso(fatoCodigo)
                    .Select(static o => new ValorDominioDeclaradoCongelado(o.Codigo, o.Rotulo, o.Ordem))]);
            case OrigemValoresColeta.MunicipiosDoBonus:
                return Result<IReadOnlyList<ValorDominioDeclaradoCongelado>>.Success([.. VocabularioDeFatos.MunicipiosDoBonus(processo)
                    .Select(static (m, ordem) => new ValorDominioDeclaradoCongelado(m.CodigoIbge, $"{m.Nome}/{m.Uf}", ordem))]);
            case OrigemValoresColeta.Catalogo:
            default:
                break;
        }

        if (fatoNoCatalogo.ValoresDominioDeclarados is { Count: > 0 } declarados)
        {
            // Ordenação canônica própria (D2): o encoder não pode depender de o catálogo já vir
            // ordenado — FatoCandidato.AdicionarValorDominio valida unicidade de Codigo, não de
            // Ordem, e duas configurações equivalentes com empate produziriam bytes distintos sem
            // o desempate por código.
            IReadOnlyList<ValorDominioDeclaradoCongelado> valores = [.. declarados
                .OrderBy(static v => v.Ordem)
                .ThenBy(static v => v.Codigo, StringComparer.Ordinal)
                .Select(static v => new ValorDominioDeclaradoCongelado(v.Codigo, v.Descricao, v.Ordem))];
            return Result<IReadOnlyList<ValorDominioDeclaradoCongelado>>.Success(valores);
        }

        return Result<IReadOnlyList<ValorDominioDeclaradoCongelado>>.Failure(new DomainError(
            "ProcessoSeletivo.FatoDeSelecaoSemOrigemDeValores",
            $"O fato coletado '{fatoCodigo}' está vinculado como seleção, mas o catálogo não declara valores de "
            + "domínio para ele e a fonte dos seus valores não é o processo nem o bônus regional — não há de onde derivar os valores selecionáveis."));
    }
}
