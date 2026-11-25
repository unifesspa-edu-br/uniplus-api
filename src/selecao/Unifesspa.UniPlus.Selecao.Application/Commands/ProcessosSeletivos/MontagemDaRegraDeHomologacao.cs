namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Monta a regra de homologação das inscrições que uma fase declara: cada condição pelas recusas do domínio, e o
/// enquadramento da condição contra o vocabulário de fatos. As recusas se acumulam por posição de entrada (ADR-0125), cada
/// uma no campo da regra que a causou. O vocabulário é lido uma vez por edição, e só quando alguma condição tem enquadramento.
/// </summary>
internal sealed class MontagemDaRegraDeHomologacao(
    LeituraMemoizada<ProcessoSeletivo, ContextoDoCatalogo> catalogos, ProcessoSeletivo processo)
{
    /// <summary>
    /// A regra da fase com as alternativas que passaram, e as recusas das demais; a fase que não a declara não homologa,
    /// e a regra é nula. A regra montada com recusas serve só para o domínio julgar o resto dela, como o resultado
    /// que aponta: quem a recebe não a grava enquanto houver recusa.
    /// </summary>
    public async Task<(RegraDeHomologacaoDasInscricoes? Regra, IReadOnlyList<FieldError> Recusas)> MontarAsync(
        RegraDeHomologacaoInput? declarada, string campo, CancellationToken cancellationToken)
    {
        if (declarada is null)
        {
            return (null, []);
        }

        MontadosEmLista<AlternativaDeCondicoes> alternativas = await AlternativasAsync(declarada.Alternativas ?? [], campo, cancellationToken)
            .ConfigureAwait(false);
        return (RegraDeHomologacaoDasInscricoes.Declarar(declarada.ResultadoAtoCodigo, alternativas.Itens), alternativas.Recusas);
    }

    private async Task<MontadosEmLista<AlternativaDeCondicoes>> AlternativasAsync(
        IReadOnlyList<AlternativaDeCondicoesInput> declaradas, string campoDaRegra, CancellationToken cancellationToken)
    {
        MontadosEmLista<AlternativaDeCondicoes> montadas = new();
        for (int posicao = 0; posicao < declaradas.Count; posicao++)
        {
            montadas.Somar(await AlternativaAsync(declaradas[posicao], $"{campoDaRegra}.alternativas[{posicao}]", cancellationToken).ConfigureAwait(false));
        }

        return montadas;
    }

    private async Task<Result<AlternativaDeCondicoes>> AlternativaAsync(
        AlternativaDeCondicoesInput declarada, string campo, CancellationToken cancellationToken)
    {
        Result<List<CondicaoDeHomologacao>> condicoes = await CondicoesAsync(declarada.Condicoes ?? [], $"{campo}.condicoes", cancellationToken)
            .ConfigureAwait(false);
        return condicoes.IsFailure
            ? Result<AlternativaDeCondicoes>.ValidationFailure(condicoes.Errors)
            : NoCampo(AlternativaDeCondicoes.Declarar(condicoes.Value!), campo);
    }

    private async Task<Result<List<CondicaoDeHomologacao>>> CondicoesAsync(
        IReadOnlyList<CondicaoDeHomologacaoInput> declaradas, string campoDasCondicoes, CancellationToken cancellationToken)
    {
        MontadosEmLista<CondicaoDeHomologacao> montadas = new();
        for (int posicao = 0; posicao < declaradas.Count; posicao++)
        {
            montadas.Somar(await CondicaoAsync(declaradas[posicao], $"{campoDasCondicoes}[{posicao}]", cancellationToken).ConfigureAwait(false));
        }

        return montadas.Resultado();
    }

    // O enquadramento recusado não impede o julgamento do resto da condição: tipo, edição e motivo são independentes dele.
    private async Task<Result<CondicaoDeHomologacao>> CondicaoAsync(
        CondicaoDeHomologacaoInput declarada, string campo, CancellationToken cancellationToken)
    {
        Result<PredicadoDnf?> enquadramento = await EnquadramentoAsync(declarada.Enquadramento, $"{campo}.enquadramento", cancellationToken)
            .ConfigureAwait(false);
        Result<CondicaoDeHomologacao> condicao = NoCampo(
            CondicaoDeHomologacao.Declarar(declarada.Tipo, declarada.EdicaoDoExame, declarada.MotivoDesfavoravelCodigo, enquadramento.Value), campo);
        return enquadramento.IsFailure || condicao.IsFailure
            ? Result<CondicaoDeHomologacao>.ValidationFailure([.. condicao.Errors, .. enquadramento.Errors])
            : condicao;
    }

    private async Task<Result<PredicadoDnf?>> EnquadramentoAsync(
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? clausulas, string campo, CancellationToken cancellationToken)
    {
        if (clausulas is { Count: 0 })
        {
            return Result<PredicadoDnf?>.Success(PredicadoDnf.SemClausula);
        }

        Result<PredicadoDnf?> predicado = EntradaDeRegras.Predicado(clausulas);
        if (predicado.IsFailure || predicado.Value is null)
        {
            return predicado.IsFailure ? RecusasDoEnquadramento(campo, predicado.Errors) : predicado;
        }

        return await ConferirEnquadramentoAsync(predicado, predicado.Value, campo, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<PredicadoDnf?>> ConferirEnquadramentoAsync(
        Result<PredicadoDnf?> predicado, PredicadoDnf enquadramento, string campo, CancellationToken cancellationToken)
    {
        ContextoDoCatalogo catalogo = (await catalogos.ObterAsync(processo, cancellationToken).ConfigureAwait(false))!;
        (Result semantica, _, TipoDeValorEsperado? tipoEsperado) = PredicadoDnfValidador.ValidarComCausa(enquadramento, catalogo.Vocabulario, null, catalogo.DominiosDinamicos);
        return semantica.IsFailure ? RecusasDoEnquadramento(campo, semantica.Errors, tipoEsperado) : predicado;
    }

    private static Result<PredicadoDnf?> RecusasDoEnquadramento(string campo, IReadOnlyList<FieldError> erros, TipoDeValorEsperado? tipoEsperado = null) =>
        Result<PredicadoDnf?>.ValidationFailure([.. erros.Select(erro => new FieldError(
            (erro with { Field = erro.Field ?? string.Empty }).NoCaminhoDe(campo).Field,
            new DomainError(erro.Error.Code, MensagemDaRecusa(erro.Error.Code, tipoEsperado))))]);

    private static string MensagemDaRecusa(string codigo, TipoDeValorEsperado? tipoEsperado) => codigo.StartsWith("CondicaoDnf.", StringComparison.Ordinal)
        ? MensagemDaForma(codigo) : codigo == "PredicadoDnf.ValorIncompativelComTipo" ? MensagemDoTipo(tipoEsperado) : MensagemDoCatalogo(codigo);

    private static string MensagemDaForma(string codigo) => codigo switch
    {
        "CondicaoDnf.FatoObrigatorio" => "Informe o fato da condição.",
        "CondicaoDnf.OperadorInvalido" => "O operador informado não é reconhecido.",
        "CondicaoDnf.FormaIncoerenteComOperador" => "O formato do valor não corresponde ao operador informado.",
        _ => "Não foi possível conferir a condição informada.",
    };

    private static string MensagemDoTipo(TipoDeValorEsperado? tipo) => tipo switch
    {
        TipoDeValorEsperado.VerdadeiroOuFalso => "Informe verdadeiro ou falso.",
        TipoDeValorEsperado.NumeroInteiro => "Informe um número inteiro.",
        TipoDeValorEsperado.Texto => "Informe um texto.",
        TipoDeValorEsperado.ListaDeTextos => "Informe uma lista de textos.",
        _ => "O valor informado não tem o tipo esperado para o fato.",
    };

    private static string MensagemDoCatalogo(string codigo) => codigo switch
    {
        "ClausulaDnf.ClausulaVazia" => "Cada cláusula deve ter ao menos uma condição.",
        "PredicadoDnf.CondicaoNula" => "Informe a condição.",
        "PredicadoDnf.FatoDesconhecido" => "O fato informado não está cadastrado.",
        "PredicadoDnf.FatoNaoColetadoPeloProcesso" => "O fato informado não é coletado por este processo.",
        "PredicadoDnf.OperadorIncompativelComDominio" => "O operador não é compatível com o fato informado.",
        "PredicadoDnf.ValorForaDoDominio" => "O valor informado não está entre os valores permitidos para o fato.",
        "PredicadoDnf.ValorIncompativelComTipo" => "O valor informado não tem o tipo esperado para o fato.",
        _ => "Não foi possível conferir a condição informada.",
    };

    // A recusa do domínio fala o campo dentro do que ela declarou; a montagem o localiza no corpo do pedido.
    private static Result<T> NoCampo<T>(Result<T> declarado, string campo) => declarado.IsSuccess
        ? declarado
        : Result<T>.ValidationFailure([.. declarado.Errors.Select(erro => (erro with { Field = erro.Field ?? string.Empty }).NoCaminhoDe(campo))]);

    /// <summary>Os itens que passaram na declaração e as recusas dos demais, acumuladas na ordem da entrada.</summary>
    private sealed class MontadosEmLista<T>
    {
        private readonly List<T> _itens = [];
        private readonly List<FieldError> _recusas = [];

        public IReadOnlyList<T> Itens => _itens;

        public IReadOnlyList<FieldError> Recusas => _recusas;

        public void Somar(Result<T> montado)
        {
            if (montado.IsSuccess)
            {
                _itens.Add(montado.Value!);
                return;
            }

            _recusas.AddRange(montado.Errors);
        }

        public Result<List<T>> Resultado() => _recusas.Count > 0
            ? Result<List<T>>.ValidationFailure(_recusas)
            : Result<List<T>>.Success(_itens);
    }
}
