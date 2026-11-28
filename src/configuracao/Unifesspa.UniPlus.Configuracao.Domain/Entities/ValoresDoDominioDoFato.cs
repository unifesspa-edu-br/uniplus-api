namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As operações sobre a lista de valores de domínio de um fato categórico do administrador (ADR-0116):
/// adicionar, corrigir, reordenar e ativar ou desativar. As conferências são de
/// <see cref="ConferenciaDosValoresDoFato"/>; esta parte grava na lista do fato e reinicia a
/// aprovação, e o fato segue dono da lista.
/// </summary>
internal sealed class ValoresDoDominioDoFato(FatoCandidato fato, List<FatoValorDominio> valores)
{
    private static readonly DomainError ValorNaoEncontrado =
        new(FatoValorDominioErrorCodes.NaoEncontrado, "Valor de domínio não encontrado neste fato.");

    private static readonly DomainError JaAtivo = new(FatoValorDominioErrorCodes.JaAtivo, "O valor já está ativo.");
    private static readonly DomainError JaDesativado = new(FatoValorDominioErrorCodes.JaDesativado, "O valor já está desativado.");

    internal Result Adicionar(string codigo, string? descricao, int? ordem, bool ativo, string? orientacao)
    {
        if (RecusaDeEstado() is { } recusa)
        {
            return Result.Failure(recusa);
        }

        Result<NovoValorDoFato> novo = ConferenciaDosValoresDoFato.ConferirNovo(codigo, descricao, orientacao, ordem, fato.Origem, CodigosDosValores);
        if (novo.IsFailure)
        {
            return Result.ValidationFailure(novo.Errors);
        }

        valores.Add(FatoValorDominio.Criar(fato.Id, novo.Value.Codigo, novo.Value.Textos.Descricao, novo.Value.Ordem, ativo, novo.Value.Textos.Orientacao));
        fato.Aprovacao.AoEditarItem(protecaoMudou: false);
        return Result.Success();
    }

    internal Result Corrigir(string codigo, string? descricao, string? orientacao)
    {
        Result<FatoValorDominio> alvo = ValorEditavel(codigo);
        if (alvo.IsFailure)
        {
            return Result.Failure(alvo.Error!);
        }
        Result<TextosDoValor> textos = ConferenciaDosValoresDoFato.ConferirTextos(fato.Origem, descricao, orientacao);
        if (textos.IsFailure)
        {
            return Result.ValidationFailure(textos.Errors);
        }
        alvo.Value!.CorrigirTextos(textos.Value.Descricao, textos.Value.Orientacao);
        fato.Aprovacao.AoEditarItem(protecaoMudou: false);
        return Result.Success();
    }

    internal Result Reordenar(IReadOnlyList<string>? codigosEmOrdem)
    {
        if (RecusaDeEstado() is { } recusa)
        {
            return Result.Failure(recusa);
        }
        Result<string[]> ordem = ConferenciaDosValoresDoFato.ConferirOrdem(codigosEmOrdem, CodigosDosValores);
        if (ordem.IsFailure)
        {
            return Result.ValidationFailure(ordem.Errors);
        }

        AplicarOrdem(ordem.Value!);
        return Result.Success();
    }

    internal Result DefinirAtivo(string codigo, bool ativo)
    {
        Result<FatoValorDominio> alvo = ValorDoFatoDoAdministrador(codigo);
        if (alvo.IsFailure)
        {
            return Result.Failure(alvo.Error!);
        }
        if (alvo.Value!.Ativo == ativo)
        {
            return Result.Failure(ativo ? JaAtivo : JaDesativado);
        }

        alvo.Value.DefinirAtivo(ativo);
        fato.Aprovacao.AoEditarItem(protecaoMudou: false);
        return Result.Success();
    }

    private IReadOnlyCollection<string> CodigosDosValores => [.. valores.Select(static v => v.Codigo)];

    // Valor na posição em que já está não é regravado: sem mudança, o EF não marca a linha e a
    // versão do fato não avança.
    private void AplicarOrdem(string[] codigosEmOrdem)
    {
        foreach ((int posicao, string codigo) in codigosEmOrdem.Index())
        {
            PorCodigo(codigo)!.DefinirOrdem(posicao);
        }

        fato.Aprovacao.AoEditarItem(protecaoMudou: false);
    }

    private Result<FatoValorDominio> ValorDoFatoDoAdministrador(string codigo)
    {
        if (RecusaDeSistema() is { } recusa)
        {
            return Result<FatoValorDominio>.Failure(recusa);
        }

        return ComoResultado(codigo);
    }

    private Result<FatoValorDominio> ValorEditavel(string codigo)
    {
        if (RecusaDeEstado() is { } recusa)
        {
            return Result<FatoValorDominio>.Failure(recusa);
        }

        return ComoResultado(codigo);
    }

    private Result<FatoValorDominio> ComoResultado(string codigo) =>
        PorCodigo(codigo) is { } valor
            ? Result<FatoValorDominio>.Success(valor)
            : Result<FatoValorDominio>.Failure(ValorNaoEncontrado);

    private DomainError? RecusaDeEstado() =>
        ConferenciaDosValoresDoFato.RecusaDeEstado(RecusaDeSistema(), fato.Dominio, fato.FatoDeMembroAgregado, fato.FonteValores);

    private DomainError? RecusaDeSistema() => fato.SemeadoPeloSistema ? RedefinicaoDoFatoDeSistema.RecusaDaDefinicao : null;

    private FatoValorDominio? PorCodigo(string codigo) =>
        valores.FirstOrDefault(v => string.Equals(v.Codigo, codigo, StringComparison.Ordinal));
}
