namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
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

/// <summary>
/// Grava o veredicto da verificação de um arquivo no documento de uma versão do perfil. Roda sob a trava do perfil,
/// como as demais operações. O veredicto não decide a vigência: a versão só entra em vigor pela aprovação humana, que
/// confere o documento verificado no momento da decisão.
/// </summary>
public sealed class ResultadoDoDocumentoDaVersao
{
    private readonly IPerfilRepository _perfis;
    private readonly IAutorizacaoUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public ResultadoDoDocumentoDaVersao(IPerfilRepository perfis, IAutorizacaoUnitOfWork unitOfWork, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        _perfis = perfis;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<DesfechoDoResultadoDaVersao> RegistrarAsync(
        ResultadoDaVerificacaoDeArquivo resultado, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        Guid? perfilId = await _perfis.ObterPerfilIdDoDocumentoAsync(resultado.ArquivoId, cancellationToken).ConfigureAwait(false);
        if (perfilId is null)
        {
            return DesfechoDoResultadoDaVersao.Desconhecido;
        }

        Perfil perfil = await _perfis.ObterParaEditarAsync(perfilId.Value, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"O perfil {perfilId} do documento {resultado.ArquivoId} não existe.");
        if (!Aplicar(perfil, resultado))
        {
            return DesfechoDoResultadoDaVersao.Ignorado;
        }

        await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return DesfechoDoResultadoDaVersao.Aplicado;
    }

    private bool Aplicar(Perfil perfil, ResultadoDaVerificacaoDeArquivo resultado) =>
        resultado.Veredicto == VeredictoDaVerificacao.Verificado
            ? perfil.RegistrarDocumentoVerificado(resultado.ArquivoId, resultado.HashSha256!, _clock.GetUtcNow())
            : perfil.RegistrarDocumentoRecusado(resultado.ArquivoId, _clock.GetUtcNow());
}
