namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A resolução contra o catálogo dos itens e dos grupos do formulário do processo, já lidos pela forma, sem ler nem
/// mudar o processo: para os itens de forma válida, a coletabilidade (só se coleta fato
/// declarado com vínculo de campo) e a semântica das regras (operador × domínio × valor do fato
/// citado, contra a oferta do próprio processo para os domínios dinâmicos). A validação estrutural
/// do grafo e o guard de mutação são do agregado.
/// </summary>
internal static class EscritaDosItens
{
    /// <summary>
    /// Os grupos conferidos contra o catálogo, acumulando tudo no mesmo lote (ADR-0125): os campos
    /// como os itens, mas coletando fato de membro; a semântica da exibição e da obrigatoriedade
    /// do grupo e a recusa de fato calculado de atributo, sempre que as regras tiverem forma. O
    /// grupo só é montado quando nada dele foi recusado.
    /// </summary>
    public static (List<GrupoColetado> Grupos, List<FieldError> Erros) ResolverGrupos(GruposLidos lidos, ContextoDoCatalogo contexto)
    {
        ArgumentNullException.ThrowIfNull(lidos);
        ArgumentNullException.ThrowIfNull(contexto);
        List<(GrupoColetado? Grupo, List<FieldError> Erros)> resolvidos =
            [.. lidos.Grupos.Select((grupo, indice) => ResolverGrupo(grupo, $"grupos[{indice}]", contexto))];
        return ([.. resolvidos.Select(static r => r.Grupo).OfType<GrupoColetado>()],
            [.. lidos.Erros, .. resolvidos.SelectMany(static r => r.Erros)]);
    }

    private static (GrupoColetado? Grupo, List<FieldError> Erros) ResolverGrupo(GrupoLido lido, string caminho, ContextoDoCatalogo contexto)
    {
        (List<FatoColetado> campos, List<FieldError> dosCampos) = Resolver(lido.Campos with { Erros = [] }, contexto, $"{caminho}.subitens", campoDeGrupo: true);
        if (lido.Configuracao is not { } configuracao)
        {
            return (null, dosCampos);
        }

        List<FieldError> doGrupo = [.. ConferenciaDoItemNoCatalogo.DoGrupo(configuracao, contexto).Select(erro => erro.NoCaminhoDe(caminho))];
        bool recusado = lido.Ocorrencias is null || doGrupo.Count > 0 || !lido.FormaValida || dosCampos.Count > 0 || lido.Campos.Erros.Count > 0;
        return (recusado ? null : CriarGrupo(lido, lido.Ocorrencias!, configuracao, campos), [.. doGrupo, .. dosCampos]);
    }

    // A forma do grupo e a identificação do candidato já foram conferidas na leitura; a fábrica as
    // repete e devolve o grupo.
    private static GrupoColetado CriarGrupo(GrupoLido lido, OcorrenciasDoGrupo ocorrencias, ExibicaoEObrigatoriedade configuracao, List<FatoColetado> campos) =>
        GrupoColetado.Criar(lido.Entrada.Codigo, lido.Entrada.Ordem, lido.Entrada.PaginaCodigo, lido.Entrada.Rotulo, ocorrencias, configuracao, campos).Value!;

    /// <summary>
    /// Os itens de forma válida conferidos contra o catálogo, com as recusas da forma e da semântica
    /// no mesmo lote. O campo de grupo coleta fato de membro; o item, fato do candidato.
    /// </summary>
    public static (List<FatoColetado> Itens, List<FieldError> Erros) Resolver(
        ItensLidos lidos, ContextoDoCatalogo contexto, string caminho = "itens", bool campoDeGrupo = false)
    {
        ArgumentNullException.ThrowIfNull(lidos);
        ArgumentNullException.ThrowIfNull(contexto);
        List<(int Indice, Result<FatoColetado> Fato)> resolvidos =
        [
            .. lidos.Itens.Select((item, indice) => (indice, item))
                .Where(static par => par.item.Configuracao is not null)
                .Select(par => (par.indice, ResolverFato(par.item.Entrada, par.item.Configuracao!, contexto, campoDeGrupo))),
        ];
        return (
            [.. resolvidos.Where(static r => r.Fato.IsSuccess).Select(static r => r.Fato.Value!)],
            [.. lidos.Erros, .. resolvidos.Where(static r => r.Fato.IsFailure).SelectMany(r => r.Fato.Errors.Select(erro => erro.NoCaminhoDe($"{caminho}[{r.Indice}]")))]);
    }

    private static Result<FatoColetado> ResolverFato(FatoColetadoInput input, ConfiguracaoDoItem configuracao, ContextoDoCatalogo contexto, bool campoDeGrupo)
    {
        Result<FatoCandidatoView> fato = FatoColetavelNoCampo(input, configuracao.TipoDeCampo, contexto, campoDeGrupo);
        if (fato.IsFailure)
        {
            return Result<FatoColetado>.ValidationFailure(fato.Errors);
        }

        (RegrasDoCampo regras, List<FieldError> erros) = ConferirSemanticaDasRegras(input, fato.Value!, configuracao, contexto);
        return CriarComAsCopiasDoCatalogo(input, configuracao.TipoDeCampo, regras, fato.Value!, erros);
    }

    /// <summary>
    /// O fato que o campo coleta, do catálogo: o item coleta fato declarado do próprio candidato; o
    /// campo de grupo, fato declarado de membro, e derivados e calculados não se respondem. O tipo
    /// de campo tem de ser coerente com o fato.
    /// </summary>
    private static Result<FatoCandidatoView> FatoColetavelNoCampo(
        FatoColetadoInput input, TipoDeCampo tipoDeCampo, ContextoDoCatalogo contexto, bool campoDeGrupo)
    {
        DomainError? naoColetavel = campoDeGrupo
            ? ConferenciaNoCatalogo.FatoDoSubitem(input.FatoCodigo, contexto.FatosDasRegras)
            : ConferenciaNoCatalogo.FatoDoItem(input.FatoCodigo, contexto.FatosDasRegras);
        if (naoColetavel is not null)
        {
            return Result<FatoCandidatoView>.ValidationFailure([new("fatoCodigo", naoColetavel)]);
        }

        FatoCandidatoView fato = contexto.Fatos[input.FatoCodigo];
        return ConferenciaDoItemNoCatalogo.Coerencia(fato, tipoDeCampo) is { } incoerencia
            ? Result<FatoCandidatoView>.ValidationFailure([new($"{FatoColetadoInput.CaminhoDaApresentacao}.tipoDeCampo", incoerencia)])
            : Result<FatoCandidatoView>.Success(fato);
    }

    /// <summary>
    /// A exibição lida da entrada e a semântica de todas as regras do item contra o catálogo. Devolve
    /// as regras com que o item é criado — sem a exibição, quando ela foi recusada, para que as recusas
    /// do próprio item saiam junto das demais — e todas as recusas.
    /// </summary>
    private static (RegrasDoCampo Regras, List<FieldError> Erros) ConferirSemanticaDasRegras(
        FatoColetadoInput input, FatoCandidatoView fato, ConfiguracaoDoItem configuracao, ContextoDoCatalogo contexto)
    {
        Result<PredicadoDnf?> exibicao = EntradaDeRegras.Predicado(input.Regras?.Exibicao);
        RegrasDoCampo lidas = configuracao.RegrasComExibicao(exibicao.IsSuccess ? exibicao.Value : null);
        IEnumerable<string> fatosDaExibicao = (input.Regras?.Exibicao ?? []).SelectMany(static c => c).Where(static c => c is not null).Select(static c => c.Fato);
        List<FieldError> erros =
        [
            .. exibicao.Errors.Select(static e => e.NoCaminhoDoItem(ConferenciaDoItemNoCatalogo.CaminhoDaExibicao)),
            .. ConferenciaDoItemNoCatalogo.Semantica(fato, configuracao.TipoDeCampo, lidas, fatosDaExibicao, contexto),
        ];
        bool exibicaoRecusada = erros.Any(static e => e.Field is { } campo && campo.StartsWith(ConferenciaDoItemNoCatalogo.CaminhoDaExibicao, StringComparison.Ordinal));
        return (exibicaoRecusada ? configuracao.RegrasComExibicao(null) : lidas, erros);
    }

    /// <summary>
    /// O item criado com as cópias tiradas do catálogo. As recusas do próprio item (coerência das
    /// restrições com o campo, autorreferência, ajuda) são acumuladas na mesma resposta que as semânticas.
    /// </summary>
    private static Result<FatoColetado> CriarComAsCopiasDoCatalogo(
        FatoColetadoInput input, TipoDeCampo tipoDeCampo, RegrasDoCampo regras, FatoCandidatoView fato, List<FieldError> erros)
    {
        Result<FatoColetado> item = FatoColetado.Criar(
            input.FatoCodigo,
            input.Ordem,
            input.PaginaCodigo,
            EntradaDeRegras.Apresentacao(input.Apresentacao, tipoDeCampo),
            regras,
            VocabularioDeFatos.CopiaNoCatalogo(fato));
        return erros.Count == 0
            ? item
            : Result<FatoColetado>.ValidationFailure([.. erros, .. item.IsFailure ? item.Errors : []]);
    }
}
