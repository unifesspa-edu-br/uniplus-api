namespace Unifesspa.UniPlus.Selecao.Application.Services.NotasDoEnem;

using System.Text.RegularExpressions;

using Commands.NotasDoEnem;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Leiautes;

using DTOs.NotasDoEnem;

using Kernel.Results;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;


/// <summary>Gera o envio pela versão divulgada e registra sua evidência, com os CPFs apenas no arquivo.</summary>
public sealed partial class GeracaoDoEnvioAoInep(ResolvedorDoEscopoDeNotasDoEnem escopos,
    IInscritosDaEdicaoDoEnem inscritos, IGeracaoDeEnvioAoInepRepository geracoes,
    IUserContext usuario, IResolvedorFusoInstitucional fuso)
{
    public async Task<Result<EnvioAoInepGeradoDto>> GerarAsync(GerarEnvioAoInepCommand command,
        ILeiauteDeArquivoDoInep leiaute, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(leiaute);
        Result<EscopoDeNotasDoEnem> escopo = await escopos.ResolverAsync(
            command.ProcessoSeletivoId, command.EtapaProcessoId, command.Edicao, true, cancellationToken).ConfigureAwait(false);
        if (escopo.IsFailure)
        {
            return Result<EnvioAoInepGeradoDto>.Failure(escopo.Error!);
        }
        DomainError? recusa = ResolvedorDoEscopoDeNotasDoEnem.ConferirInscricoesEncerradas(escopo.Value!, agora, fuso);
        return recusa is not null ? Result<EnvioAoInepGeradoDto>.Failure(recusa)
            : await GerarArquivoAsync(command, leiaute, agora, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<EnvioAoInepGeradoDto>> GerarArquivoAsync(GerarEnvioAoInepCommand command,
        ILeiauteDeArquivoDoInep leiaute, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> cpfs = await inscritos.ListarCpfsAsync(command.ProcessoSeletivoId, command.Edicao, cancellationToken).ConfigureAwait(false);
        if (cpfs.Count == 0)
        {
            return Result<EnvioAoInepGeradoDto>.Failure(new DomainError(NotasDoEnemErrorCodes.EdicaoSemInscritos,
                "Nenhuma inscrição enviada escolheu esta edição, então não há CPF a enviar ao INEP."));
        }
        byte[] arquivo = EscritorDeArquivoDeEnvio.Escrever((LeiauteDeEnvioDoInep)leiaute, [.. cpfs.Order(StringComparer.Ordinal)]);
        GeracaoDeEnvioAoInep geracao = GeracaoDeEnvioAoInep.Registrar(command.ProcessoSeletivoId,
            command.EtapaProcessoId, command.Edicao, leiaute.Codigo, leiaute.Versao, leiaute.Hash,
            usuario.UserId ?? string.Empty, agora, cpfs.Count, usuario.NomeCompleto);
        await geracoes.AdicionarAsync(geracao, cancellationToken).ConfigureAwait(false);
        return Result<EnvioAoInepGeradoDto>.Success(new(arquivo, NomeDoArquivo(command.Edicao), geracao.Id, geracao.TotalDeCpfs));
    }

    private static string NomeDoArquivo(string edicao) => $"envio-inep-enem-{NaoSeguro().Replace(edicao, "_")}.txt";

    [GeneratedRegex("[^A-Za-z0-9_-]")]
    private static partial Regex NaoSeguro();
}
