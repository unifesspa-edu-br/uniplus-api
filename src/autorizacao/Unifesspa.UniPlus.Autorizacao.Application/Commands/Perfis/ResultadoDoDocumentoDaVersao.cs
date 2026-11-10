namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>O que o resultado da verificação de um arquivo fez ao documento de uma versão.</summary>
public enum DesfechoDoResultadoDaVersao
{
    /// <summary>Nenhum documento de versão tem esse id.</summary>
    Desconhecido = 1,

    /// <summary>Resultado repetido, ou documento que não esperava veredicto: nada mudou.</summary>
    Ignorado = 2,

    /// <summary>O veredicto e o hash foram gravados no documento.</summary>
    Aplicado = 3,
}

/// <summary>O que o veredicto fez ao documento e os avisos que a gravação dele pede.</summary>
public sealed record RegistroDoResultadoDaVersao(DesfechoDoResultadoDaVersao Desfecho, IReadOnlyList<EnviarEmail> Avisos)
{
    public static RegistroDoResultadoDaVersao Sem(DesfechoDoResultadoDaVersao desfecho) => new(desfecho, []);
}

/// <summary>
/// Grava o veredicto da verificação de um arquivo no documento de uma versão do perfil. Roda na ordem de travas única
/// das demais operações, que materializa o prazo vencido da pendente e o grava mesmo quando o veredicto é ignorado. O
/// veredicto não decide a vigência: a versão só entra em vigor pela aprovação humana, que confere o documento verificado
/// no momento da decisão.
/// </summary>
public sealed class ResultadoDoDocumentoDaVersao
{
    private readonly IPerfilRepository _perfis;
    private readonly OperacaoSobreOPerfil _operacao;
    private readonly AvisoDeEtapaApta _etapaApta;

    public ResultadoDoDocumentoDaVersao(IPerfilRepository perfis, OperacaoSobreOPerfil operacao, AvisoDeEtapaApta etapaApta)
    {
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(operacao);
        ArgumentNullException.ThrowIfNull(etapaApta);

        _perfis = perfis;
        _operacao = operacao;
        _etapaApta = etapaApta;
    }

    public async Task<RegistroDoResultadoDaVersao> RegistrarAsync(
        ResultadoDaVerificacaoDeArquivo resultado, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        Guid? perfilId = await _perfis.ObterPerfilIdDoDocumentoAsync(resultado.ArquivoId, cancellationToken).ConfigureAwait(false);
        if (perfilId is null)
        {
            return RegistroDoResultadoDaVersao.Sem(DesfechoDoResultadoDaVersao.Desconhecido);
        }

        GravacaoDaOperacao<RegistroDoResultadoDaVersao> gravacao = await _operacao.ExecutarComAvisosSemChaveAsync(
            perfilId.Value, (perfil, contexto) => RegistrarNoPerfilAsync(perfil, resultado, contexto.Agora, cancellationToken), cancellationToken)
            .ConfigureAwait(false);

        return gravacao.Resultado.IsSuccess
            ? gravacao.Resultado.Value! with { Avisos = gravacao.Avisos }
            : throw new InvalidOperationException($"O perfil {perfilId} do documento {resultado.ArquivoId} não existe.");
    }

    private async Task<DesfechoDaOperacao<RegistroDoResultadoDaVersao>> RegistrarNoPerfilAsync(
        Perfil perfil, ResultadoDaVerificacaoDeArquivo resultado, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        DesfechoDoResultadoDaVersao desfecho = Aplicar(perfil, resultado, agora);
        IReadOnlyList<EnviarEmail> avisos = desfecho == DesfechoDoResultadoDaVersao.Aplicado
            ? await _etapaApta.EmitirAsync(perfil, cancellationToken).ConfigureAwait(false)
            : [];

        return new DesfechoDaOperacao<RegistroDoResultadoDaVersao>(Result<RegistroDoResultadoDaVersao>.Success(RegistroDoResultadoDaVersao.Sem(desfecho)), Avisos: avisos);
    }

    private static DesfechoDoResultadoDaVersao Aplicar(Perfil perfil, ResultadoDaVerificacaoDeArquivo resultado, DateTimeOffset agora)
    {
        bool aplicado = resultado.Veredicto == VeredictoDaVerificacao.Verificado
            ? perfil.RegistrarDocumentoVerificado(resultado.ArquivoId, resultado.HashSha256!, agora)
            : perfil.RegistrarDocumentoRecusado(resultado.ArquivoId, agora);

        return aplicado ? DesfechoDoResultadoDaVersao.Aplicado : DesfechoDoResultadoDaVersao.Ignorado;
    }
}
