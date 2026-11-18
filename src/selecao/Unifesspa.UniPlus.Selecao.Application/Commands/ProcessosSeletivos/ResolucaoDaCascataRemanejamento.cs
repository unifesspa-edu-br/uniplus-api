namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ProcessosSeletivos.Vagas;

using Kernel.Results;

/// <summary>Resolve e confere a cascata solicitada antes da alteração do processo.</summary>
internal static class ResolucaoDaCascataRemanejamento
{
    internal static async Task<Result<ConfiguracaoCascataRemanejamento?>> ResolverAsync(
        DefinirCascataRemanejamentoCommand pedido, IRegraCatalogoReader catalogo, CancellationToken cancellationToken)
    {
        if (TudoAusente(pedido))
        {
            return Result<ConfiguracaoCascataRemanejamento?>.Success(null);
        }

        if (!TudoPresente(pedido))
        {
            return CamposObrigatorios();
        }

        Result<List<DestinoRemanejamento>> destinos = LerDestinos(pedido);
        return destinos.IsFailure
            ? Result<ConfiguracaoCascataRemanejamento?>.ValidationFailure(destinos.Errors)
            : await ResolverRegraAsync(pedido, destinos.Value!, catalogo, cancellationToken).ConfigureAwait(false);
    }

    private static Result<ConfiguracaoCascataRemanejamento?> CamposObrigatorios() =>
        Result<ConfiguracaoCascataRemanejamento?>.Failure(new DomainError(
            "ConfiguracaoCascataRemanejamento.CamposObrigatorios",
            "A regra, sua versão, o destino final e os destinos devem ser informados juntos ou deixados vazios."));

    private static bool TudoAusente(DefinirCascataRemanejamentoCommand pedido) =>
        pedido.RegraCodigo is null && pedido.RegraVersao is null && pedido.FallbackCodigo is null && pedido.Destinos is null;

    private static bool TudoPresente(DefinirCascataRemanejamentoCommand pedido) =>
        pedido.RegraCodigo is not null && pedido.RegraVersao is not null && pedido.FallbackCodigo is not null && pedido.Destinos is not null;

    private static Result<List<DestinoRemanejamento>> LerDestinos(DefinirCascataRemanejamentoCommand pedido)
    {
        List<DestinoRemanejamento> destinos = [];
        List<FieldError> erros = [];
        for (int indice = 0; indice < pedido.Destinos!.Count; indice++)
        {
            LerDestino(pedido.Destinos[indice], indice, destinos, erros);
        }

        if (erros.Count > 0)
        {
            return RecusarItensInvalidos(pedido, erros);
        }

        return ConferirForma(pedido.FallbackCodigo!, destinos);
    }

    private static Result<List<DestinoRemanejamento>> ConferirForma(string fallback, List<DestinoRemanejamento> destinos)
    {
        List<FieldError> forma = ConfiguracaoCascataRemanejamento.ValidarForma(fallback, destinos);
        return forma.Count > 0 ? Result<List<DestinoRemanejamento>>.ValidationFailure(forma)
            : Result<List<DestinoRemanejamento>>.Success(destinos);
    }

    private static Result<List<DestinoRemanejamento>> RecusarItensInvalidos(
        DefinirCascataRemanejamentoCommand pedido, List<FieldError> erros)
    {
        List<FieldError> independentes = ConfiguracaoCascataRemanejamento.ValidarFallbackELimitesIndependentesDeItens(
            pedido.FallbackCodigo, pedido.Destinos!.Count, pedido.Destinos.Select(d => d?.ModalidadeOrigemCodigo));
        return Result<List<DestinoRemanejamento>>.ValidationFailure([.. independentes, .. erros]);
    }

    private static void LerDestino(DestinoRemanejamentoInput? item, int indice,
        List<DestinoRemanejamento> destinos, List<FieldError> erros)
    {
        if (item is null)
        {
            erros.Add(ItemAusente(indice));
            return;
        }

        Result<DestinoRemanejamento> destino = DestinoRemanejamento.Criar(item.ModalidadeOrigemCodigo, item.Ordem, item.ModalidadeDestinoCodigo);
        if (destino.IsFailure)
        {
            erros.AddRange(destino.Errors.Select(erro => erro with { Field = $"destinos[{indice}].{erro.Field}" }));
            return;
        }

        destinos.Add(destino.Value!);
    }

    private static FieldError ItemAusente(int indice) => new($"destinos[{indice}]", new DomainError(
        "ConfiguracaoCascataRemanejamento.CamposObrigatorios", "Nenhum item da lista de destinos pode ser nulo."));

    private static async Task<Result<ConfiguracaoCascataRemanejamento?>> ResolverRegraAsync(
        DefinirCascataRemanejamentoCommand pedido, List<DestinoRemanejamento> destinos,
        IRegraCatalogoReader catalogo, CancellationToken cancellationToken)
    {
        RegraCatalogo? regra = await catalogo.ObterAsync(pedido.RegraCodigo!, pedido.RegraVersao!, cancellationToken)
            .ConfigureAwait(false);
        if (regra is null)
        {
            return RegraNaoEncontrada();
        }

        if (regra.Tipo != TipoRegra.CriterioRemanejamento)
        {
            return RegraDeTipoInvalido();
        }

        return CriarCascata(pedido, destinos, regra);
    }

    private static Result<ConfiguracaoCascataRemanejamento?> RegraNaoEncontrada() =>
        Result<ConfiguracaoCascataRemanejamento?>.Failure(new DomainError(
            "ConfiguracaoCascataRemanejamento.RegraNaoEncontrada",
            "Regra de remanejamento não encontrada no catálogo de regras."));

    private static Result<ConfiguracaoCascataRemanejamento?> RegraDeTipoInvalido() =>
        Result<ConfiguracaoCascataRemanejamento?>.Failure(new DomainError(
            "ConfiguracaoCascataRemanejamento.RegraTipoInvalido",
            "A regra informada não é uma regra de remanejamento."));

    private static Result<ConfiguracaoCascataRemanejamento?> CriarCascata(
        DefinirCascataRemanejamentoCommand pedido, List<DestinoRemanejamento> destinos, RegraCatalogo regra)
    {
        Result<ConfiguracaoCascataRemanejamento> resultado = ConfiguracaoCascataRemanejamento.Criar(
            regra.Referencia(), pedido.FallbackCodigo!, destinos);
        if (resultado.IsFailure)
        {
            return Result<ConfiguracaoCascataRemanejamento?>.ValidationFailure(resultado.Errors);
        }

        if (!ConformidadeDaCascataComARegra.Conferir(resultado.Value!, regra))
        {
            return MatrizDivergente();
        }

        return Result<ConfiguracaoCascataRemanejamento?>.Success(resultado.Value);
    }
    private static Result<ConfiguracaoCascataRemanejamento?> MatrizDivergente() =>
        Result<ConfiguracaoCascataRemanejamento?>.Failure(new DomainError(
            "ConfiguracaoCascataRemanejamento.MatrizDivergenteDaRegra",
            "O destino final ou os destinos informados divergem da declaração da regra."));

}
