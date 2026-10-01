namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

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
/// filtrado: entram os valores ativos e os desativados que uma condição do processo já cita, porque
/// desativar recusa só vínculo novo (ADR-0136). Para as demais, decide a origem copiada para o
/// campo coletado (<c>ProcessoSeletivo.OrigemDasOpcoes</c>), a mesma que o agregado confere na
/// publicação: as opções do PRÓPRIO processo, pela mesma leitura que valida predicado
/// (<c>ProcessoSeletivo.OpcoesDoProcesso</c>); ou os municípios do bônus regional, com o código
/// IBGE como valor, "Município/UF" como rótulo e a ordem alfabética de nome de
/// <c>VocabularioDeFatos.MunicipiosDoBonus</c>.
/// </para>
/// <para>
/// Não faz I/O próprio — recebe o catálogo já lido UMA vez pelo handler (D4-bis), o mesmo
/// compartilhado com a conferência de coletabilidade e o resolvedor de metadado de fato.
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
        IReadOnlySet<(string Fato, string Valor)> valoresCitados = processo.Vinculos().Valores;
        foreach (FatoColetado fato in processo.FatosColetados)
        {
            if (!fato.TipoRenderizacao.EhSelecao())
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
                ResolverValoresDoFato(fato, fatoNoCatalogo, processo, valoresCitados);
            if (valoresDoFato.IsFailure)
            {
                return Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>>.Failure(
                    valoresDoFato.Error!);
            }

            valoresPorFato[fato.FatoCodigo] = valoresDoFato.Value;
        }

        if (RespostaForaDasOpcoes(processo, valoresPorFato) is { } foraDasOpcoes)
        {
            return Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>>.Failure(foraDasOpcoes);
        }

        return Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>>.Success(valoresPorFato);
    }

    /// <summary>
    /// Toda resposta de um campo que forma as opções de outro precisa ser opção desse outro. As
    /// opções que o processo declara podem mudar depois de os campos serem definidos, e é aqui que
    /// as ofertas dos dois campos congelam juntas.
    /// </summary>
    private static DomainError? RespostaForaDasOpcoes(
        ProcessoSeletivo processo,
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresPorFato)
    {
        foreach (FatoColetado alvo in processo.FatosColetados)
        {
            if (alvo.Restricoes.OfType<OpcoesDasRespostas>().SingleOrDefault() is not { } respostas)
            {
                continue;
            }

            HashSet<string> oferta = [.. (valoresPorFato[alvo.FatoCodigo] ?? []).Select(static v => v.Codigo)];
            foreach (string fonte in respostas.Fatos)
            {
                if (valoresPorFato.GetValueOrDefault(fonte) is { } daFonte && daFonte.Any(v => !oferta.Contains(v.Codigo)))
                {
                    return new DomainError(
                        FatoColetadoErrorCodes.OpcoesDeOutroDominio,
                        $"As opções do campo '{fonte}' formam as opções do campo '{alvo.FatoCodigo}', mas nem todas são opções dele.");
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Não é defesa em profundidade: um fato categórico DECLARADO de cardinalidade escalar/
    /// multivalorada, sem <c>ValoresDominioDeclarados</c> no catálogo e com código diferente dos
    /// dois categóricos de escopo-processo hoje conhecidos, é alcançável por dado — um fato novo
    /// no catálogo (migration futura), vinculado como <c>SELECAO_UNICA</c>/<c>SELECAO_MULTIPLA</c>
    /// porque <c>CoerenciaDeRenderizacao</c> só confere domínio/cardinalidade, não a origem dos
    /// valores. A conferência de coletabilidade (o fato existe e é coletável) não recusa esse
    /// caso — por isso ele devolve <see cref="DomainError"/>, não lança.
    /// </summary>
    private static Result<IReadOnlyList<ValorDominioDeclaradoCongelado>> ResolverValoresDoFato(
        FatoColetado fato, FatoCandidatoView fatoNoCatalogo, ProcessoSeletivo processo,
        IReadOnlySet<(string Fato, string Valor)> valoresCitados)
    {
        string fatoCodigo = fato.FatoCodigo;
        Result<IReadOnlyList<ValorDominioDeclaradoCongelado>> resolvido =
            ResolverOrigemDosValores(fato, fatoNoCatalogo, processo, valoresCitados);
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
        FatoColetado fato, FatoCandidatoView fatoNoCatalogo, ProcessoSeletivo processo,
        IReadOnlySet<(string Fato, string Valor)> valoresCitados)
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

        if (fatoNoCatalogo.ValoresDominioDeclarados is { Count: > 0 })
        {
            IReadOnlyList<FatoValorDominioViewItem> vigentes = VocabularioDeFatos.ValoresVigentes(fatoNoCatalogo, valoresCitados);
            // Ordenação canônica própria (D2): o encoder não pode depender de o catálogo já vir
            // ordenado — FatoCandidato.AdicionarValorDominio valida unicidade de Codigo, não de
            // Ordem, e duas configurações equivalentes com empate produziriam bytes distintos sem
            // o desempate por código.
            IReadOnlyList<ValorDominioDeclaradoCongelado> valores = [.. vigentes
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
