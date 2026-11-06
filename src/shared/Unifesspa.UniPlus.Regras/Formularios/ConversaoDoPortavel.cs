namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A definição avaliável de um formulário portável. Cada elemento é conferido onde o construtor dele
/// o exigiria, por meio da <c>Violacao</c> do próprio tipo, e a recusa sai como texto para quem
/// importou: o construtor só protege quem monta a definição pelo módulo. A leitura para na primeira
/// recusa, na ordem etapas, termos, derivações, agregados e, por fim, a definição: o que vem depois
/// dela não chega a ser lido.
/// </summary>
internal static class ConversaoDoPortavel
{
    internal static Result<DefinicaoFormulario> Converter(FormularioPortavel portavel)
    {
        List<DefinicaoEtapa> etapas = [];
        List<DefinicaoTermo> termos = [];
        List<DerivacaoDoFato> derivacoes = [];
        List<DefinicaoAgregado> agregados = [];
        DomainError? erro = Ler(portavel.Etapas, Etapa, etapas) ?? Ler(portavel.Termos, Termo, termos)
            ?? Ler(portavel.Derivacoes, Derivacao, derivacoes) ?? Ler(portavel.Agregados, Agregado, agregados)
            ?? Recusa(DefinicaoFormulario.Violacao(etapas, termos, derivacoes, agregados));

        return Montado(erro, () => new DefinicaoFormulario(etapas, termos, derivacoes, agregados));
    }

    private static Result<DefinicaoEtapa> Etapa(EtapaPortavel etapa)
    {
        Result<PredicadoDnf?> exibicao = DoFio(etapa.Exibicao);
        List<DefinicaoItem> itens = [];
        List<DefinicaoGrupo> grupos = [];
        DomainError? erro = exibicao.Error ?? Ler(etapa.Itens, Item, itens) ?? Ler(etapa.Grupos, Grupo, grupos)
            ?? Recusa(DefinicaoEtapa.Violacao(etapa.Codigo));

        return Montado(erro, () => new DefinicaoEtapa(etapa.Codigo, exibicao.Value, itens, grupos));
    }

    private static Result<DefinicaoGrupo> Grupo(GrupoPortavel grupo)
    {
        Result<PredicadoDnf?> exibicao = DoFio(grupo.Exibicao);
        Result<Obrigatoriedade> obrigatoriedade = Obrigatoriedade(grupo.Obrigatoriedade, grupo.PredicadoObrigatoriedade, $"grupo '{grupo.Codigo}'");
        List<DefinicaoItem> subitens = [];
        DomainError? erro = exibicao.Error ?? obrigatoriedade.Error ?? Ler(grupo.Subitens, Item, subitens)
            ?? Recusa(DefinicaoGrupo.Violacao(grupo.Codigo, grupo.Minimo, grupo.Maximo));

        return Montado(erro, () => new DefinicaoGrupo(
            grupo.Codigo, exibicao.Value, obrigatoriedade.Value!, grupo.Minimo, grupo.Maximo, subitens, grupo.IncluiCandidato));
    }

    private static Result<DefinicaoItem> Item(ItemPortavel item)
    {
        Result<PredicadoDnf?> exibicao = DoFio(item.Exibicao);
        Result<Obrigatoriedade> obrigatoriedade = Obrigatoriedade(item.Obrigatoriedade, item.PredicadoObrigatoriedade, $"campo '{item.FatoCodigo}'");
        Result<Impedimento?> impedimento = EntradaDeRegras.Impedimento(item.Impedimento);
        List<RestricaoValor> restricoes = [];
        DomainError? erro = exibicao.Error ?? obrigatoriedade.Error ?? impedimento.Error
            ?? Ler(item.Restricoes, EntradaDeRegras.Restricao, restricoes) ?? Recusa(DefinicaoItem.Violacao(item.FatoCodigo, item.Formato));

        return Montado(erro, () => new DefinicaoItem(
            item.FatoCodigo, exibicao.Value, obrigatoriedade.Value!, restricoes, impedimento.Value, item.Formato));
    }

    private static Result<DefinicaoTermo> Termo(TermoPortavel termo)
    {
        Result<PredicadoDnf?> exibicao = DoFio(termo.Exibicao);
        Result<Obrigatoriedade> obrigatoriedade = Obrigatoriedade(termo.Obrigatoriedade, termo.PredicadoObrigatoriedade, $"termo '{termo.Codigo}'");
        DomainError? erro = exibicao.Error ?? obrigatoriedade.Error ?? Recusa(DefinicaoTermo.Violacao(termo.Codigo));

        return Montado(erro, () => new DefinicaoTermo(termo.Codigo, exibicao.Value, obrigatoriedade.Value!));
    }

    private static Result<DefinicaoAgregado> Agregado(AgregadoPortavel agregado)
    {
        OperacaoAgregado operacao = AgregadoDeGrupo.DoToken(agregado.Operacao);
        DomainError? erro = operacao == OperacaoAgregado.Nenhuma
            ? Recusa(FormularioPortavelErrorCodes.OperacaoDeAgregadoInvalida, $"A operação do agregado '{agregado.Codigo}' é EXISTE ou VALORES_PRESENTES.")
            : Recusa(DefinicaoAgregado.Violacao(agregado.Codigo, agregado.GrupoCodigo, agregado.FatoDeMembro));

        return Montado(erro, () => new DefinicaoAgregado(agregado.Codigo, agregado.GrupoCodigo, agregado.FatoDeMembro, operacao));
    }

    private static Result<DerivacaoDoFato> Derivacao(DerivacaoPortavel derivacao)
    {
        List<RegraDerivacao> regras = [];
        DomainError? erro = Ler(derivacao.Regras, regra => Regra(derivacao, regra), regras) ?? Recusa(DerivacaoDoFato.Violacao(derivacao.FatoCodigo));

        return erro is not null ? Result<DerivacaoDoFato>.Failure(erro) : DerivacaoDeRegras(derivacao, regras);
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
            ? Result<RegraDerivacao>.Failure(quando.Error!)
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

    private static Result<Obrigatoriedade> Obrigatoriedade(
        string? token, IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? predicado, string dono)
    {
        Result<PredicadoDnf?> quando = DoFio(predicado);
        if (quando.IsFailure)
        {
            return Result<Obrigatoriedade>.Failure(quando.Error!);
        }

        return EntradaDeRegras.Obrigatoriedade(token, quando.Value) is { } obrigatoriedade
            ? Result<Obrigatoriedade>.Success(obrigatoriedade)
            : Result<Obrigatoriedade>.Failure(new DomainError(
                FormularioPortavelErrorCodes.ObrigatoriedadeInvalida,
                $"A obrigatoriedade do {dono} é SEMPRE ou NUNCA sem predicado, ou QUANDO com ele."));
    }

    /// <summary>O predicado do fio: nulo é sem condição, e a lista vazia é o predicado sem cláusula.</summary>
    private static Result<PredicadoDnf?> DoFio(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? clausulas)
    {
        if (clausulas is { Count: 0 })
        {
            Result<PredicadoDnf> semClausula = PredicadoDnf.CriarDeCondicoesAgrupadas([]);
            return Result<PredicadoDnf?>.Success(semClausula.Value);
        }

        return EntradaDeRegras.Predicado(clausulas);
    }

    /// <summary>Converte cada elemento para o destino, na ordem, e devolve a primeira recusa; lista nula é lista vazia.</summary>
    private static DomainError? Ler<TEntrada, TSaida>(IEnumerable<TEntrada>? origem, Func<TEntrada, Result<TSaida>> converter, List<TSaida> destino)
    {
        foreach (TEntrada elemento in origem ?? [])
        {
            Result<TSaida> lido = converter(elemento);
            if (lido.IsFailure)
            {
                return lido.Error;
            }

            destino.Add(lido.Value!);
        }

        return null;
    }

    private static Result<T> Montado<T>(DomainError? recusa, Func<T> criar) =>
        recusa is null ? Result<T>.Success(criar()) : Result<T>.Failure(recusa);

    private static DomainError? Recusa(string? violacao) =>
        violacao is null ? null : Recusa(FormularioPortavelErrorCodes.EstruturaInvalida, violacao);

    private static DomainError Recusa(string codigo, string mensagem) => new(codigo, mensagem);
}
