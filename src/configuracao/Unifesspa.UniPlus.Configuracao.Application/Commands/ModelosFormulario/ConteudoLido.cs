namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O conteúdo do modelo lido da escrita, com a posição de cada parte na entrada: as recusas da
/// leitura e as da conferência contra o catálogo apontam o índice que veio. A parte que não se leu
/// fica de fora, com a recusa dela.
/// </summary>
internal sealed record ConteudoLido(
    string? Titulo,
    IReadOnlyList<(int Indice, EtapaDoModelo Etapa)> Etapas,
    IReadOnlyList<(int Indice, ItemDoModelo Item)> Itens,
    IReadOnlyList<(int Indice, TermoDoModelo Termo)> Termos,
    IReadOnlyList<string> Pressupostos,
    IReadOnlyList<FieldError> Erros)
{
    public ConteudoDoModelo ParaConteudo() => new(
        Titulo, [.. Etapas.Select(static e => e.Etapa)], [.. Itens.Select(static i => i.Item)], [.. Termos.Select(static t => t.Termo)], Pressupostos);

    /// <summary>
    /// Lê a entrada pela forma das regras do formulário (<see cref="EntradaDeRegras"/>): token
    /// desconhecido vira o sentinela, que a forma recusa depois; predicado, obrigatoriedade e
    /// restrição malformados são recusados aqui. O formato do campo de texto é o do fato no
    /// catálogo.
    /// </summary>
    public static ConteudoLido Ler(ConteudoDoModeloInput? entrada, IReadOnlyDictionary<string, string> formatos)
    {
        ArgumentNullException.ThrowIfNull(formatos);
        List<FieldError> erros = [];
        List<(int, EtapaDoModelo)> etapas = [];
        List<(int, ItemDoModelo)> itens = [];
        List<(int, TermoDoModelo)> termos = [];

        IReadOnlyList<EtapaFormularioInput?> etapasDeEntrada = entrada?.Etapas ?? [];
        for (int i = 0; i < etapasDeEntrada.Count; i++)
        {
            if (LerEtapa(etapasDeEntrada[i], $"etapas[{i}]", erros) is { } etapa)
            {
                etapas.Add((i, etapa));
            }
        }

        IReadOnlyList<FatoColetadoInput?> itensDeEntrada = entrada?.Itens ?? [];
        for (int i = 0; i < itensDeEntrada.Count; i++)
        {
            if (LerItem(itensDeEntrada[i], $"itens[{i}]", formatos, erros) is { } item)
            {
                itens.Add((i, item));
            }
        }

        IReadOnlyList<TermoExigidoInput?> termosDeEntrada = entrada?.Termos ?? [];
        for (int i = 0; i < termosDeEntrada.Count; i++)
        {
            if (LerTermo(termosDeEntrada[i], $"termos[{i}]", erros) is { } termo)
            {
                termos.Add((i, termo));
            }
        }

        return new(entrada?.Titulo, etapas, itens, termos, [.. (entrada?.Pressupostos ?? []).Select(static p => p ?? string.Empty)], erros);
    }

    private static EtapaDoModelo? LerEtapa(EtapaFormularioInput? entrada, string campo, List<FieldError> erros)
    {
        if (entrada is null)
        {
            erros.Add(Nula(campo));
            return null;
        }

        PredicadoDnf? exibicao = LerPredicado(entrada.Exibicao, $"{campo}.exibicao", erros, out bool lido);
        return lido
            ? new EtapaDoModelo(
                entrada.Codigo, entrada.Ordem, EstruturaFormulario.TipoDoToken(entrada.Tipo), EstruturaFormulario.BlocoDoToken(entrada.Bloco),
                entrada.Titulo, entrada.Descricao, entrada.Aviso, exibicao)
            : null;
    }

    private static ItemDoModelo? LerItem(
        FatoColetadoInput? entrada, string campo, IReadOnlyDictionary<string, string> formatos, List<FieldError> erros)
    {
        if (entrada is null)
        {
            erros.Add(Nula(campo));
            return null;
        }

        int recusasAntes = erros.Count;
        PredicadoDnf? exibicao = LerPredicado(entrada.Precondicao, $"{campo}.precondicao", erros, out _);
        Obrigatoriedade? obrigatoriedade = LerObrigatoriedade(
            entrada.Obrigatoriedade, entrada.PredicadoObrigatoriedade, campo, erros);
        List<RestricaoValor> restricoes = [];
        IReadOnlyList<RestricaoValorInput?> restricoesDeEntrada = entrada.Restricoes ?? [];
        for (int j = 0; j < restricoesDeEntrada.Count; j++)
        {
            Result<RestricaoValor> restricao = restricoesDeEntrada[j] is { } r
                ? EntradaDeRegras.Restricao(r)
                : Result<RestricaoValor>.Failure(new DomainError(RestricaoValorErrorCodes.TipoDesconhecido, "A restrição de valor é nula."));
            if (restricao.IsSuccess)
            {
                restricoes.Add(restricao.Value!);
            }
            else
            {
                erros.Add(new($"{campo}.restricoes[{j}]", restricao.Error!));
            }
        }

        if (erros.Count > recusasAntes)
        {
            return null;
        }

        string? formato = entrada.FatoCodigo is { } codigo && formatos.TryGetValue(ModeloFormulario.CodigoNaFormaGravada(codigo), out string? doCatalogo) ? doCatalogo : null;
        return new ItemDoModelo(
            entrada.FatoCodigo, entrada.Ordem, entrada.EtapaCodigo, entrada.Rotulo, TipoRenderizacaoCodigo.FromCodigo(entrada.TipoRenderizacao),
            formato, entrada.Ajuda, obrigatoriedade!, exibicao, restricoes, entrada.PedirConfirmacao);
    }

    private static TermoDoModelo? LerTermo(TermoExigidoInput? entrada, string campo, List<FieldError> erros)
    {
        if (entrada is null)
        {
            erros.Add(Nula(campo));
            return null;
        }

        int recusasAntes = erros.Count;
        PredicadoDnf? exibicao = LerPredicado(entrada.Exibicao, $"{campo}.exibicao", erros, out _);
        Obrigatoriedade? obrigatoriedade = LerObrigatoriedade(entrada.Obrigatoriedade, entrada.PredicadoObrigatoriedade, campo, erros);
        return erros.Count > recusasAntes
            ? null
            : new TermoDoModelo(entrada.Codigo, entrada.Ordem, entrada.TermoId, entrada.VersaoId, exibicao, obrigatoriedade!);
    }

    private static PredicadoDnf? LerPredicado(
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? entrada, string campo, List<FieldError> erros, out bool lido)
    {
        Result<PredicadoDnf?> predicado = EntradaDeRegras.Predicado(entrada);
        lido = predicado.IsSuccess;
        if (!lido)
        {
            erros.Add(new(campo, predicado.Error!));
        }

        return predicado.Value;
    }

    private static Obrigatoriedade? LerObrigatoriedade(
        string? token, IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? predicadoDeEntrada, string campo, List<FieldError> erros)
    {
        PredicadoDnf? predicado = LerPredicado(predicadoDeEntrada, $"{campo}.predicadoObrigatoriedade", erros, out bool lido);
        if (!lido)
        {
            return null;
        }

        if (EntradaDeRegras.Obrigatoriedade(token, predicado) is { } obrigatoriedade)
        {
            return obrigatoriedade;
        }

        erros.Add(new($"{campo}.obrigatoriedade", new DomainError(
            ModeloFormularioErrorCodes.ObrigatoriedadeInvalida,
            "A obrigatoriedade é SEMPRE ou NUNCA, sem predicado, ou QUANDO, com predicado.")));
        return null;
    }

    private static FieldError Nula(string campo) => new(campo, new DomainError(
        ModeloFormularioErrorCodes.EntradaMalformada, "A etapa, o item ou o termo veio nulo."));
}
