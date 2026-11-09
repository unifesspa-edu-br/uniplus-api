namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>O que o resultado da verificação de um arquivo fez ao documento de uma versão.</summary>
public enum DesfechoDoResultadoDaVersao
{
    /// <summary>Nenhum documento de versão tem esse id.</summary>
    Desconhecido = 1,

    /// <summary>Resultado repetido, ou documento que não esperava veredicto: nada mudou.</summary>
    Ignorado = 2,

    /// <summary>O veredicto foi gravado; a versão pode ter entrado em vigor ou sido recusada.</summary>
    Aplicado = 3,
}

/// <summary>
/// Aplica o veredicto da verificação ao documento de uma versão do perfil. Roda na ordem de travas das demais
/// operações: acha o perfil pelo documento, trava-o, recarrega o agregado e só então decide. A entrada em vigor é a
/// decisão do agregado sobre o veredicto "verificado" (<see cref="Perfil.RegistrarDocumentoVerificado"/>).
/// </summary>
/// <remarks>
/// Não captura conflito de gravação: a exceção desfaz a transação inteira e a mensagem é reentregue, e na reentrega as
/// conferências leem o estado já confirmado.
/// </remarks>
public sealed class ResultadoDoDocumentoDaVersao
{
    private readonly IPerfilRepository _perfis;
    private readonly LeitorDoContextoDaVersao _leitor;
    private readonly IAutorizacaoUnitOfWork _unitOfWork;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;
    private readonly TimeProvider _clock;

    public ResultadoDoDocumentoDaVersao(
        IPerfilRepository perfis,
        LeitorDoContextoDaVersao leitor,
        IAutorizacaoUnitOfWork unitOfWork,
        ICatalogoDePermissoesConsultavel catalogo,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(leitor);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(clock);

        _perfis = perfis;
        _leitor = leitor;
        _unitOfWork = unitOfWork;
        _catalogo = catalogo;
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

        if (!await AplicarAsync(perfil, resultado, cancellationToken).ConfigureAwait(false))
        {
            return DesfechoDoResultadoDaVersao.Ignorado;
        }

        await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return DesfechoDoResultadoDaVersao.Aplicado;
    }

    private async Task<bool> AplicarAsync(Perfil perfil, ResultadoDaVerificacaoDeArquivo resultado, CancellationToken cancellationToken)
    {
        if (resultado.Veredicto != VeredictoDaVerificacao.Verificado)
        {
            return perfil.RegistrarDocumentoRecusado(resultado.ArquivoId, _clock.GetUtcNow());
        }

        string nomeDaVersao = perfil.Versoes.First(v => v.Documentos.Any(d => d.Id == resultado.ArquivoId)).Nome;
        ContextoDaVersao contexto = await _leitor.LerAsync(perfil.Id, nomeDaVersao, cancellationToken).ConfigureAwait(false);

        return perfil.RegistrarDocumentoVerificado(
            resultado.ArquivoId, resultado.HashSha256!, _clock.GetUtcNow(), contexto, _catalogo).Desfecho != DesfechoDoDocumentoVerificado.Ignorado;
    }
}
