namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Leituras;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A definição avaliável de um formulário portável. Cada elemento é conferido onde o construtor dele
/// o exigiria, por meio da <c>Violacao</c> do próprio tipo, e a recusa sai como texto para quem
/// importou: o construtor só protege quem monta a definição pelo módulo. A leitura para na primeira
/// recusa, na ordem páginas, termos, derivações, agregados e, por fim, a definição: o que vem depois
/// dela não chega a ser lido.
/// </summary>
internal static class ConversaoDoPortavel
{
    internal static Result<DefinicaoFormulario> Converter(FormularioPortavel portavel)
    {
        List<DefinicaoPagina> paginas = [];
        List<DefinicaoTermo> termos = [];
        List<DerivacaoDoFato> derivacoes = [];
        List<DefinicaoAgregado> agregados = [];
        IReadOnlyList<FieldError>? erro = Ler(portavel.Paginas, Pagina, paginas) ?? Ler(portavel.Termos, Termo, termos)
            ?? Ler(portavel.Derivacoes, Derivacao, derivacoes) ?? Ler(portavel.Agregados, Agregado, agregados)
            ?? Recusa(DefinicaoFormulario.Violacao(paginas, termos, derivacoes, agregados));

        return Montado(erro, () => new DefinicaoFormulario(paginas, termos, derivacoes, agregados));
    }

    private static Result<DefinicaoPagina> Pagina(PaginaPortavel pagina)
    {
        Result<PredicadoDnf?> exibicao = DoFio(pagina.Exibicao);
        List<DefinicaoItem> itens = [];
        List<DefinicaoGrupo> grupos = [];
        IReadOnlyList<FieldError>? erro = Falhas(exibicao) ?? Ler(pagina.Itens, Item, itens) ?? Ler(pagina.Grupos, Grupo, grupos)
            ?? Recusa(DefinicaoPagina.Violacao(pagina.Codigo));

        return Montado(erro, () => new DefinicaoPagina(pagina.Codigo, exibicao.Value, itens, grupos));
    }

    private static Result<DefinicaoGrupo> Grupo(GrupoPortavel grupo)
    {
        Result<PredicadoDnf?> exibicao = DoFio(grupo.Regras?.Exibicao);
        Result<Obrigatoriedade> obrigatoriedade = Obrigatoriedade(grupo.Regras?.Obrigatoriedade, $"grupo '{grupo.Codigo}'");
        OcorrenciasDoGrupoDto ocorrencias = grupo.Ocorrencias;
        List<DefinicaoItem> subitens = [];
        IReadOnlyList<FieldError>? erro = Falhas(exibicao) ?? Falhas(obrigatoriedade) ?? Ler(grupo.Subitens, Item, subitens)
            ?? Recusa(DefinicaoGrupo.Violacao(grupo.Codigo, ocorrencias.Minimo, ocorrencias.Maximo));

        return Montado(erro, () => new DefinicaoGrupo(
            grupo.Codigo, exibicao.Value, obrigatoriedade.Value!, ocorrencias.Minimo, ocorrencias.Maximo, subitens, ocorrencias.IncluiCandidato));
    }

    private static Result<DefinicaoItem> Item(ItemPortavel item)
    {
        RegrasDoCampoDto? regras = item.Regras;
        Result<PredicadoDnf?> exibicao = DoFio(regras?.Exibicao);
        Result<Obrigatoriedade> obrigatoriedade = Obrigatoriedade(regras?.Obrigatoriedade, $"campo '{item.FatoCodigo}'");
        Result<Impedimento?> impedimento = EntradaDeRegras.Impedimento(LeituraDeRegras.ParaEntrada(regras?.Impedimento));
        List<RestricaoValor> restricoes = [];
        IReadOnlyList<FieldError>? erro = Falhas(exibicao) ?? Falhas(obrigatoriedade) ?? Falhas(impedimento)
            ?? Ler(regras?.Restricoes, Restricao, restricoes) ?? Recusa(DefinicaoItem.Violacao(item.FatoCodigo, item.Formato));

        return Montado(erro, () => new DefinicaoItem(
            item.FatoCodigo, exibicao.Value, obrigatoriedade.Value!, restricoes, impedimento.Value, item.Formato));
    }

    private static Result<RestricaoValor> Restricao(RestricaoValorDto restricao) =>
        EntradaDeRegras.Restricao(LeituraDeRegras.ParaEntrada(restricao));

    private static Result<DefinicaoTermo> Termo(TermoPortavel termo)
    {
        Result<PredicadoDnf?> exibicao = DoFio(termo.Regras?.Exibicao);
        Result<Obrigatoriedade> obrigatoriedade = Obrigatoriedade(termo.Regras?.Obrigatoriedade, $"termo '{termo.Codigo}'");
        IReadOnlyList<FieldError>? erro = Falhas(exibicao) ?? Falhas(obrigatoriedade) ?? Recusa(DefinicaoTermo.Violacao(termo.Codigo));

        return Montado(erro, () => new DefinicaoTermo(termo.Codigo, exibicao.Value, obrigatoriedade.Value!));
    }

    private static Result<DefinicaoAgregado> Agregado(AgregadoPortavel agregado)
    {
        bool conhecida = CatalogoDe<OperacaoAgregado>.TentarObter(agregado.Operacao, out OperacaoAgregado operacao);
        IReadOnlyList<FieldError>? erro = !conhecida
            ? Recusa(FormularioPortavelErrorCodes.OperacaoDeAgregadoInvalida, $"A operação do agregado '{agregado.Codigo}' é {string.Join(" ou ", CatalogoDe<OperacaoAgregado>.Todos().Select(static d => d.Codigo))}.")
            : Recusa(DefinicaoAgregado.Violacao(agregado.Codigo, agregado.GrupoCodigo, agregado.FatoDeMembro));

        return Montado(erro, () => new DefinicaoAgregado(agregado.Codigo, agregado.GrupoCodigo, agregado.FatoDeMembro, operacao));
    }

    private static Result<DerivacaoDoFato> Derivacao(DerivacaoPortavel derivacao)
    {
        List<RegraDerivacao> regras = [];
        IReadOnlyList<FieldError>? erro = Ler(derivacao.Regras, regra => Regra(derivacao, regra), regras) ?? Recusa(DerivacaoDoFato.Violacao(derivacao.FatoCodigo));

        return erro is not null ? Result<DerivacaoDoFato>.ValidationFailure(erro) : DerivacaoDeRegras(derivacao, regras);
    }

    private static Result<RegraDerivacao> Regra(DerivacaoPortavel derivacao, RegraDerivacaoPortavel regra)
    {
        if (regra.Quando is null)
        {
            return Result<RegraDerivacao>.Failure(new DomainError(
                FormularioPortavelErrorCodes.DerivacaoInvalida,
                $"Uma regra de '{derivacao.FatoCodigo}' não diz quando ativa; a regra sempre ativa tem a lista vazia."));
        }

        Result<PredicadoDnf?> quando = DoFio(regra.Quando);
        return quando.IsFailure
            ? Result<RegraDerivacao>.ValidationFailure(quando.Errors)
            : derivacao.Booleano
                ? Result<RegraDerivacao>.Success(RegraDerivacao.CriarBooleana(quando.Value!))
                : RegraDerivacao.Criar(quando.Value!, regra.Contribui ?? string.Empty);
    }

    /// <summary>
    /// As dependências são as que as regras citam, e o domínio do categórico é o que elas contribuem: o
    /// avaliador não usa mais que isso do derivado.
    /// </summary>
    private static Result<DerivacaoDoFato> DerivacaoDeRegras(DerivacaoPortavel derivacao, List<RegraDerivacao> regras)
    {
        string[] dependencias = [.. regras.SelectMany(static r => r.FatosCitados).Distinct(StringComparer.Ordinal)];
        Result<DerivacaoDoFato> lida = derivacao.Booleano
            ? DerivacaoDoFato.CriarBooleana(derivacao.FatoCodigo, regras, dependencias)
            : DerivacaoDoFato.Criar(
                derivacao.FatoCodigo, regras, dependencias, [.. regras.Select(static r => r.Contribui!).Distinct(StringComparer.Ordinal)]);
        return lida.IsSuccess
            ? lida
            : Result<DerivacaoDoFato>.Failure(new DomainError(FormularioPortavelErrorCodes.DerivacaoInvalida, lida.Error!.Message));
    }

    private static Result<Obrigatoriedade> Obrigatoriedade(ObrigatoriedadeDto? obrigatoriedade, string dono)
    {
        Result<PredicadoDnf?> quando = DoFio(obrigatoriedade?.Predicado);
        if (quando.IsFailure)
        {
            return Result<Obrigatoriedade>.ValidationFailure(quando.Errors);
        }

        return EntradaDeRegras.Obrigatoriedade(obrigatoriedade?.Tipo, quando.Value) is { } valida
            ? Result<Obrigatoriedade>.Success(valida)
            : Result<Obrigatoriedade>.Failure(new DomainError(
                FormularioPortavelErrorCodes.ObrigatoriedadeInvalida,
                $"A obrigatoriedade do {dono} é SEMPRE ou NUNCA sem predicado, ou QUANDO com ele."));
    }

    /// <summary>O predicado do fio: nulo é sem condição, e a lista vazia é o predicado sem cláusula.</summary>
    private static Result<PredicadoDnf?> DoFio(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? clausulas)
    {
        if (clausulas is { Count: 0 })
        {
            Result<PredicadoDnf> semClausula = PredicadoDnf.CriarDeCondicoesAgrupadas([]);
            return Result<PredicadoDnf?>.Success(semClausula.Value);
        }

        return EntradaDeRegras.Predicado(LeituraDeRegras.ParaEntrada(clausulas));
    }

    /// <summary>Converte cada elemento para o destino, na ordem, e devolve a primeira recusa; lista nula é lista vazia.</summary>
    private static IReadOnlyList<FieldError>? Ler<TEntrada, TSaida>(IEnumerable<TEntrada>? origem, Func<TEntrada, Result<TSaida>> converter, List<TSaida> destino)
    {
        foreach (TEntrada elemento in origem ?? [])
        {
            Result<TSaida> lido = converter(elemento);
            if (lido.IsFailure)
            {
                return lido.Errors;
            }

            destino.Add(lido.Value!);
        }

        return null;
    }

    private static Result<T> Montado<T>(IReadOnlyList<FieldError>? recusa, Func<T> criar) =>
        recusa is null ? Result<T>.Success(criar()) : Result<T>.ValidationFailure(recusa);

    private static IReadOnlyList<FieldError>? Recusa(string? violacao) =>
        violacao is null ? null : Recusa(FormularioPortavelErrorCodes.EstruturaInvalida, violacao);

    private static IReadOnlyList<FieldError> Recusa(string codigo, string mensagem) => [new(null, new DomainError(codigo, mensagem))];

    private static IReadOnlyList<FieldError>? Falhas<T>(Result<T> resultado) => resultado.IsFailure ? resultado.Errors : null;
}
