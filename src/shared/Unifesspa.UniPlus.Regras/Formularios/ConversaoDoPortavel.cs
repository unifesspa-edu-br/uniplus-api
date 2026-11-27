namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Leituras;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A definição avaliável de um formulário portável. Cada elemento é conferido onde o construtor dele o exigiria,
/// por meio da <c>Violacao</c> do próprio tipo, e a recusa sai como texto para quem importou: o construtor só protege
/// quem monta a definição pelo módulo. A leitura acumula: todas as listas são lidas inteiras, na ordem páginas,
/// termos, derivações e agregados, e cada elemento devolve todas as suas recusas, cada uma no campo do elemento
/// recusado. Um elemento só é montado sem recusa; só deixa de ser lido o que depende de um dado recusado, como a
/// forma da obrigatoriedade quando a condição dela foi recusada. A definição do conjunto, que confere códigos
/// repetidos e referências, só é conferida quando nenhum elemento foi recusado, e a recusa dela não tem campo.
/// </summary>
internal static class ConversaoDoPortavel
{
    internal static Result<DefinicaoFormulario> Converter(FormularioPortavel portavel)
    {
        RecusasDoElemento recusas = new();
        List<DefinicaoPagina> paginas = recusas.Lista(portavel.Paginas, "paginas", pagina => Pagina(pagina!));
        List<DefinicaoTermo> termos = recusas.Lista(portavel.Termos, "termos", termo => Termo(termo!));
        List<DerivacaoDoFato> derivacoes = recusas.Lista(portavel.Derivacoes, "derivacoes", derivacao => ConversaoDaDerivacaoPortavel.Derivacao(derivacao!));
        List<DefinicaoAgregado> agregados = recusas.Lista(portavel.Agregados, "agregados", agregado => Agregado(agregado!));

        return recusas.ConferidoPor(() => Conjunto(paginas, termos, derivacoes, agregados));
    }

    /// <summary>A recusa de estrutura inválida com o texto da <c>Violacao</c> do tipo; nulo quando não há violação.</summary>
    internal static DomainError? Estrutura(string? violacao) =>
        violacao is null ? null : new DomainError(FormularioPortavelErrorCodes.EstruturaInvalida, violacao);

    private static Result<DefinicaoFormulario> Conjunto(
        List<DefinicaoPagina> paginas, List<DefinicaoTermo> termos, List<DerivacaoDoFato> derivacoes, List<DefinicaoAgregado> agregados) =>
        Estrutura(DefinicaoFormulario.Violacao(paginas, termos, derivacoes, agregados)) is { } recusa
            ? Result<DefinicaoFormulario>.Failure(recusa)
            : Result<DefinicaoFormulario>.Success(new DefinicaoFormulario(paginas, termos, derivacoes, agregados));

    private static Result<DefinicaoPagina> Pagina(PaginaPortavel pagina)
    {
        RecusasDoElemento recusas = new();
        PredicadoDnf? exibicao = recusas.Parte(RegrasDoPortavel.Predicado(pagina.Exibicao), "exibicao");
        List<DefinicaoItem> itens = recusas.Lista(pagina.Itens, "itens", item => Item(item!));
        List<DefinicaoGrupo> grupos = recusas.Lista(pagina.Grupos, "grupos", grupo => Grupo(grupo!));
        recusas.Recusar(Estrutura(DefinicaoPagina.Violacao(pagina.Codigo)), "codigo");

        return recusas.Montado(() => new DefinicaoPagina(pagina.Codigo, exibicao, itens, grupos));
    }

    private static Result<DefinicaoGrupo> Grupo(GrupoPortavel grupo)
    {
        RecusasDoElemento recusas = new();
        OcorrenciasDoGrupoDto ocorrencias = grupo.Ocorrencias;
        PredicadoDnf? exibicao = recusas.Parte(RegrasDoPortavel.Predicado(grupo.Regras?.Exibicao), "regras.exibicao");
        Obrigatoriedade? obrigatoriedade = recusas.Parte(RegrasDoPortavel.Obrigatoriedade(grupo.Regras?.Obrigatoriedade), "regras.obrigatoriedade");
        List<DefinicaoItem> subitens = recusas.Lista(grupo.Subitens, "subitens", item => Item(item!));
        recusas.Recusar(Estrutura(DefinicaoGrupo.Violacao(grupo.Codigo, ocorrencias.Minimo, ocorrencias.Maximo)));

        return recusas.Montado(() => new DefinicaoGrupo(
            grupo.Codigo, exibicao, obrigatoriedade!, ocorrencias.Minimo, ocorrencias.Maximo, subitens, ocorrencias.IncluiCandidato));
    }

    private static Result<DefinicaoItem> Item(ItemPortavel item)
    {
        RecusasDoElemento recusas = new();
        RegrasDoCampoDto? regras = item.Regras;
        PredicadoDnf? exibicao = recusas.Parte(RegrasDoPortavel.Predicado(regras?.Exibicao), "regras.exibicao");
        Obrigatoriedade? obrigatoriedade = recusas.Parte(RegrasDoPortavel.Obrigatoriedade(regras?.Obrigatoriedade), "regras.obrigatoriedade");
        Impedimento? impedimento = recusas.Parte(
            EntradaDeRegras.Impedimento(LeituraDeRegras.ParaEntrada(regras?.Impedimento)), "regras.impedimento.quando");
        List<RestricaoValor> restricoes = recusas.Lista(regras?.Restricoes, "regras.restricoes", Restricao);
        recusas.Recusar(Estrutura(DefinicaoItem.Violacao(item.FatoCodigo, item.Formato)));

        return recusas.Montado(() => new DefinicaoItem(item.FatoCodigo, exibicao, obrigatoriedade!, restricoes, impedimento, item.Formato));
    }

    private static Result<RestricaoValor> Restricao(RestricaoValorDto? restricao) =>
        EntradaDeRegras.Restricao(LeituraDeRegras.ParaEntrada(restricao!));

    private static Result<DefinicaoTermo> Termo(TermoPortavel termo)
    {
        RecusasDoElemento recusas = new();
        PredicadoDnf? exibicao = recusas.Parte(RegrasDoPortavel.Predicado(termo.Regras?.Exibicao), "regras.exibicao");
        Obrigatoriedade? obrigatoriedade = recusas.Parte(RegrasDoPortavel.Obrigatoriedade(termo.Regras?.Obrigatoriedade), "regras.obrigatoriedade");
        recusas.Recusar(Estrutura(DefinicaoTermo.Violacao(termo.Codigo)), "codigo");

        return recusas.Montado(() => new DefinicaoTermo(termo.Codigo, exibicao, obrigatoriedade!));
    }

    private static Result<DefinicaoAgregado> Agregado(AgregadoPortavel agregado)
    {
        RecusasDoElemento recusas = new();
        bool conhecida = CatalogoDe<OperacaoAgregado>.TentarObter(agregado.Operacao, out OperacaoAgregado operacao);
        if (!conhecida)
        {
            recusas.Recusar(new DomainError(
                FormularioPortavelErrorCodes.OperacaoDeAgregadoInvalida,
                $"A operação do agregado '{agregado.Codigo}' é {string.Join(" ou ", CatalogoDe<OperacaoAgregado>.Todos().Select(static d => d.Codigo))}."), "operacao");
        }

        recusas.Recusar(Estrutura(DefinicaoAgregado.Violacao(agregado.Codigo, agregado.GrupoCodigo, agregado.FatoDeMembro)));

        return recusas.Montado(() => new DefinicaoAgregado(agregado.Codigo, agregado.GrupoCodigo, agregado.FatoDeMembro, operacao));
    }
}
