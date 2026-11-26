namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;
using Domain.ProcessosSeletivos.Bancas;
using Domain.ProcessosSeletivos.Etapas;
using Domain.ValueObjects;

using Kernel.Domain.CodigosDeContrato;
using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Declara as bancas de uma etapa e acumula as violações da regra de deliberação de todas elas. A banca nova ou que troca de
/// tipo copia o tipo de banca do cadastro; a que continua com o mesmo tipo mantém a cópia que já tem, sem ler o cadastro.
/// </summary>
internal sealed class DeclaracaoDasBancasDaEtapa(LeituraMemoizada<Guid, TipoBancaView> tiposDeBanca, ChavesDasBancasDoProcesso chaves)
{
    public async Task<Result> DeclararAsync(EtapaProcesso etapa, EtapaProcessoInput input, CancellationToken cancellationToken)
    {
        Result<List<BancaDaEtapa>> declaradas = await DeclaradasAsync(etapa, input, cancellationToken).ConfigureAwait(false);
        if (declaradas.IsFailure)
        {
            return Result.ValidationFailure(declaradas.Errors);
        }

        etapa.DefinirBancas(declaradas.Value!);

        return Result.Success();
    }

    private async Task<Result<List<BancaDaEtapa>>> DeclaradasAsync(EtapaProcesso etapa, EtapaProcessoInput input, CancellationToken cancellationToken)
    {
        RecusasAcumuladas<BancaDaEtapa> bancas = new();
        for (int posicao = 0; posicao < input.Bancas.Count; posicao++)
        {
            bancas.Juntar(await BancaAsync(input.Bancas[posicao], etapa.Bancas, posicao, cancellationToken).ConfigureAwait(false));
        }

        bancas.Acrescentar(IdentificadorDasBancas.Repetidos(input.Bancas.Select(static banca => banca.Id), EtapaDeclarada.ListaDasBancas, IdentificadorDasBancas.DaEtapa));

        return bancas.Resultado();
    }

    private async Task<Result<BancaDaEtapa>> BancaAsync(
        BancaDaEtapaInput declarada, IReadOnlyCollection<BancaDaEtapa> gravadas, int posicao, CancellationToken cancellationToken)
    {
        TipoDeBancaCopiado? tipo = await TipoAsync(declarada, gravadas, cancellationToken).ConfigureAwait(false);
        string banca = $"{EtapaDeclarada.ListaDasBancas}[{posicao}]";
        Result<RegraDeDeliberacao?> regra = MontagemDaRegraDeDeliberacao.Montar(declarada.RegraDeDeliberacao, banca);
        Result<ConsequenciaDoDescumprimento?> consequencia = ConsequenciaDaAusenciaNaBanca.Ler(declarada.ConsequenciaDaAusencia, $"{banca}.consequenciaDaAusencia");
        FieldError[] erros = [.. TipoNaoEncontrado(tipo, banca), .. regra.Errors, .. consequencia.Errors, .. ViolacaoDaConsequencia(tipo, consequencia.Value, banca)];
        return tipo is null || erros.Length > 0
            ? Result<BancaDaEtapa>.ValidationFailure(erros)
            : Result<BancaDaEtapa>.Success(BancaDaEtapa.Criar(gravadas.FirstOrDefault(gravada => gravada.Id == declarada.Id)?.Chave ?? chaves.Formar(tipo.Codigo.Codigo()), tipo, regra.Value, declarada.Id, consequencia.Value));
    }

    private static FieldError[] TipoNaoEncontrado(TipoDeBancaCopiado? tipo, string banca) => tipo is not null ? [] :
    [
        new($"{banca}.tipoBancaId", new DomainError(
            EtapaProcessoErrorCodes.TipoBancaNaoEncontradoOuInativo,
            "O tipo de banca indicado não existe ou está desativado no cadastro.")),
    ];

    private static IReadOnlyList<FieldError> ViolacaoDaConsequencia(TipoDeBancaCopiado? tipo, ConsequenciaDoDescumprimento? consequencia, string banca) =>
        tipo is null ? [] : ConsequenciaDaAusenciaNaBanca.Violacao(tipo, consequencia, $"{banca}.consequenciaDaAusencia");

    private async Task<TipoDeBancaCopiado?> TipoAsync(
        BancaDaEtapaInput declarada, IReadOnlyCollection<BancaDaEtapa> gravadas, CancellationToken cancellationToken)
    {
        if (CopiaMantidaDaBanca.De(gravadas, declarada.Id, declarada.TipoBancaId) is { } mantida)
        {
            return mantida;
        }

        TipoBancaView? tipo = await tiposDeBanca.ObterAsync(declarada.TipoBancaId, cancellationToken).ConfigureAwait(false);

        return tipo is null ? null : CopiaDoTipoDeBanca.De(tipo);
    }
}
