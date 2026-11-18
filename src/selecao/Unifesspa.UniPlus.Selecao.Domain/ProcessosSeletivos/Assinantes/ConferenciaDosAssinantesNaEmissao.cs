namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Assinantes;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A conferência de quem assina, no instante de emitir o documento da fase. O documento não sai sem
/// assinantes, e cada posição é conferida de novo contra o cadastro e a estrutura de agora: a lista
/// que passou na configuração pode ter envelhecido. Passando, devolve a cópia que a emissão grava.
/// </summary>
internal static class ConferenciaDosAssinantesNaEmissao
{
    internal const string CaminhoDaLista = "/assinantes";

    internal static Result<AssinantesDaEmissao> Conferir(
        AssinantesDaFase assinantes,
        Guid unidadeDaFase,
        IReadOnlyDictionary<string, PessoaDoCadastroDeAssinantes> cadastro,
        EstruturaDasUnidadesAtivas estrutura)
    {
        ArgumentNullException.ThrowIfNull(assinantes);
        ArgumentNullException.ThrowIfNull(cadastro);

        if (assinantes.Codigos.Count == 0)
        {
            return Result<AssinantesDaEmissao>.Failure(new DomainError(
                AssinantesErrorCodes.EmissaoSemAssinantes, "O documento da fase não é emitido sem assinantes."));
        }

        Result conferencia = RegrasDosAssinantes.Conferir(assinantes.Codigos, unidadeDaFase, cadastro, estrutura, CaminhoDaLista);

        return conferencia.IsFailure
            ? Result<AssinantesDaEmissao>.ValidationFailure(conferencia.Errors)
            : Result<AssinantesDaEmissao>.Success(AssinantesDaEmissao.Criar(assinantes.Codigos.Select(codigo => cadastro[codigo])));
    }
}
