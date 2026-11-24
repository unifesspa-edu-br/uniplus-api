namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

internal sealed class CadastrosDaClassificacao(IRegraCatalogoReader regraCatalogoReader, IPesoAreaEnemReader pesoAreaEnemReader)
{
    public async Task<Result<DeclaracaoDaClassificacao>> ResolverRegrasAsync(DefinirClassificacaoCommand command, CancellationToken cancellationToken)
    {
        Result<DeclaracaoDaClassificacao> calculoEOrdem = await ResolverCalculoEOrdemDeAlocacaoAsync(command, cancellationToken).ConfigureAwait(false);
        if (calculoEOrdem.IsFailure)
        {
            return calculoEOrdem;
        }

        Result<IReadOnlyList<RegraEliminacao>> eliminacoes = await ResolverEliminacoesAsync(command, cancellationToken).ConfigureAwait(false);
        return eliminacoes.IsFailure
            ? Result<DeclaracaoDaClassificacao>.Failure(eliminacoes.Error!)
            : Result<DeclaracaoDaClassificacao>.Success(calculoEOrdem.Value! with { RegrasEliminacao = eliminacoes.Value! });
    }

    /// <summary>
    /// Consulta apenas quando a combinação aceita a resolução; a forma é conferida antes da busca,
    /// pois o banco recusa caracteres nulos. As demais combinações são conferidas pelo domínio.
    /// </summary>
    public async Task<QuadroDePesosResolvido> ResolverQuadroAsync(
        ReferenciaRegra regraCalculo, DefinirClassificacaoCommand command, CancellationToken cancellationToken) =>
        ConfiguracaoClassificacao.ExigeQuadroPesoAreaEnem(regraCalculo, command.BaseadoEmEnem)
            && !string.IsNullOrWhiteSpace(command.ResolucaoPesoAreaEnem)
            ? await CongelarQuadroAsync(command.ResolucaoPesoAreaEnem, cancellationToken).ConfigureAwait(false)
            : QuadroDePesosResolvido.SemConsulta(command.ResolucaoPesoAreaEnem);

    private async Task<Result<DeclaracaoDaClassificacao>> ResolverCalculoEOrdemDeAlocacaoAsync(
        DefinirClassificacaoCommand command, CancellationToken cancellationToken)
    {
        Result<(ReferenciaRegra Calculo, ReferenciaRegra? Arredondamento)> calculo = await ResolverCalculoDaNotaAsync(command, cancellationToken).ConfigureAwait(false);
        if (calculo.IsFailure)
        {
            return Result<DeclaracaoDaClassificacao>.Failure(calculo.Error!);
        }

        Result<ReferenciaRegra> ordem = await ResolverRegraAsync(command.RegraOrdemAlocacaoCodigo, command.RegraOrdemAlocacaoVersao,
            TipoRegra.RegraOrdemAlocacao, cancellationToken).ConfigureAwait(false);
        return ordem.IsFailure ? Result<DeclaracaoDaClassificacao>.Failure(ordem.Error!)
            : Result<DeclaracaoDaClassificacao>.Success(DeclaracaoDoPedido(command, calculo.Value, ordem.Value!));
    }

    private async Task<Result<(ReferenciaRegra Calculo, ReferenciaRegra? Arredondamento)>> ResolverCalculoDaNotaAsync(
        DefinirClassificacaoCommand command, CancellationToken cancellationToken)
    {
        Result<ReferenciaRegra> calculo = await ResolverRegraAsync(command.RegraCalculoCodigo, command.RegraCalculoVersao,
            TipoRegra.RegraCalculo, cancellationToken).ConfigureAwait(false);
        if (calculo.IsFailure)
        {
            return Result<(ReferenciaRegra, ReferenciaRegra?)>.Failure(calculo.Error!);
        }

        Result<ReferenciaRegra>? arredondamento = await ResolverArredondamentoAsync(command, cancellationToken).ConfigureAwait(false);
        return arredondamento is { IsFailure: true } ? Result<(ReferenciaRegra, ReferenciaRegra?)>.Failure(arredondamento.Error!)
            : Result<(ReferenciaRegra, ReferenciaRegra?)>.Success((calculo.Value!, arredondamento?.Value));
    }

    private async Task<Result<ReferenciaRegra>?> ResolverArredondamentoAsync(DefinirClassificacaoCommand command, CancellationToken cancellationToken) =>
        (command.RegraArredondamentoCodigo, command.RegraArredondamentoVersao) switch
        {
            (null, _) => null,
            (_, null) => Result<ReferenciaRegra>.Failure(new DomainError("ConfiguracaoClassificacao.RegraArredondamentoVersaoObrigatoria",
                "Versão da regra de arredondamento é obrigatória quando o código é informado.")),
            (string codigo, string versao) => await ResolverRegraAsync(codigo, versao, TipoRegra.RegraArredondamento, cancellationToken).ConfigureAwait(false),
        };

    private async Task<Result<ReferenciaRegra>> ResolverRegraAsync(string codigo, string versao, TipoRegra tipoEsperado, CancellationToken cancellationToken)
    {
        RegraCatalogo? regra = await regraCatalogoReader.ObterAsync(codigo, versao, cancellationToken).ConfigureAwait(false);
        return regra switch
        {
            null => Result<ReferenciaRegra>.Failure(new DomainError("ConfiguracaoClassificacao.RegraNaoEncontrada",
                $"Regra {codigo}/{versao} não encontrada no catálogo de regras.")),
            { Tipo: var tipo } when tipo != tipoEsperado => Result<ReferenciaRegra>.Failure(new DomainError(
                "ConfiguracaoClassificacao.RegraTipoInvalido", $"A regra {codigo}/{versao} não é do tipo esperado.")),
            _ => Result<ReferenciaRegra>.Success(regra.Referencia()),
        };
    }

    private async Task<Result<IReadOnlyList<RegraEliminacao>>> ResolverEliminacoesAsync(
        DefinirClassificacaoCommand command, CancellationToken cancellationToken)
    {
        List<RegraEliminacao> regras = [];
        foreach (RegraEliminacaoInput input in command.RegrasEliminacao)
        {
            Result<RegraEliminacao> resultado = await ResolverEliminacaoAsync(input, cancellationToken).ConfigureAwait(false);
            if (resultado.IsFailure)
            {
                return Result<IReadOnlyList<RegraEliminacao>>.Failure(resultado.Error!);
            }

            regras.Add(resultado.Value!);
        }

        return Result<IReadOnlyList<RegraEliminacao>>.Success(regras);
    }

    private async Task<Result<RegraEliminacao>> ResolverEliminacaoAsync(RegraEliminacaoInput input, CancellationToken cancellationToken)
    {
        RegraCatalogo? regra = await regraCatalogoReader.ObterAsync(input.RegraCodigo, input.RegraVersao, cancellationToken).ConfigureAwait(false);
        if (RecusaDaRegraDeEliminacao(input, regra) is { } recusa)
        {
            return Result<RegraEliminacao>.Failure(recusa);
        }

        Result<ParametrosRegraEliminacao> parametros = ParametrosDaEliminacaoDeclarados.Montar(input);
        return parametros.IsFailure ? Result<RegraEliminacao>.Failure(parametros.Error!)
            : RegraEliminacao.Criar(regra!.Referencia(), parametros.Value!);
    }

    private static DomainError? RecusaDaRegraDeEliminacao(RegraEliminacaoInput input, RegraCatalogo? regra) => regra switch
    {
        null => new DomainError("RegraEliminacao.RegraNaoEncontrada",
            $"Regra de eliminação {input.RegraCodigo}/{input.RegraVersao} não encontrada no catálogo de regras."),
        { Tipo: not TipoRegra.RegraEliminacao } => new DomainError("RegraEliminacao.RegraTipoInvalido",
            $"A regra {input.RegraCodigo}/{input.RegraVersao} não é do tipo regra_eliminacao."),
        _ => null,
    };

    private async Task<QuadroDePesosResolvido> CongelarQuadroAsync(string resolucao, CancellationToken cancellationToken)
    {
        List<FieldError> recusas = ConfiguracaoClassificacao.ValidarResolucaoPesoAreaEnem(resolucao);
        if (recusas.Count > 0)
        {
            return QuadroDePesosResolvido.Recusado(resolucao, recusas);
        }

        ResolucaoPesoAreaEnemView? cadastrada = await pesoAreaEnemReader.ObterPorResolucaoAsync(resolucao, cancellationToken).ConfigureAwait(false);
        return QuadroDePesosResolvido.DaResolucaoCadastrada(resolucao, cadastrada);
    }

    private static DeclaracaoDaClassificacao DeclaracaoDoPedido(
        DefinirClassificacaoCommand command, (ReferenciaRegra Calculo, ReferenciaRegra? Arredondamento) calculoDaNota, ReferenciaRegra ordem) => new()
        {
            RegraCalculo = calculoDaNota.Calculo,
            RegraArredondamento = calculoDaNota.Arredondamento,
            CasasArredondamento = command.CasasArredondamento,
            RegraOrdemAlocacao = ordem,
            BaseadoEmEnem = command.BaseadoEmEnem,
            EscalaNotaFinal = command.EscalaNotaFinal,
        };
}
