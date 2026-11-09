namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Email;
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

/// <summary>O desfecho do resultado e os avisos que a gravação dele pede, para entrarem no outbox junto dela.</summary>
public sealed record RegistroDoResultadoDaVersao(DesfechoDoResultadoDaVersao Desfecho, IReadOnlyList<EnviarEmail> Mensagens)
{
    public static RegistroDoResultadoDaVersao Sem(DesfechoDoResultadoDaVersao desfecho) => new(desfecho, []);
}

/// <summary>
/// Aplica o veredicto da verificação ao documento de uma versão do perfil. Roda na ordem de travas das demais
/// operações: acha o perfil pelo documento, trava-o, recarrega o agregado e só então decide. A entrada em vigor é a
/// decisão do agregado sobre o veredicto "verificado" (<see cref="Perfil.RegistrarDocumentoVerificado"/>), e os efeitos
/// dela sobre concessões e pedidos rodam na mesma transação, sob a mesma trava (<see cref="EfeitosDaTrocaDeVigencia"/>).
/// </summary>
/// <remarks>
/// Não captura conflito de gravação: a exceção desfaz a transação inteira e a mensagem é reentregue, e na reentrega as
/// conferências leem o estado já confirmado.
/// </remarks>
public sealed class ResultadoDoDocumentoDaVersao
{
    private readonly IPerfilRepository _perfis;
    private readonly LeitorDoContextoDaVersao _leitor;
    private readonly EfeitosDaTrocaDeVigencia _efeitos;
    private readonly IAutorizacaoUnitOfWork _unitOfWork;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;
    private readonly TimeProvider _clock;

    public ResultadoDoDocumentoDaVersao(
        IPerfilRepository perfis,
        LeitorDoContextoDaVersao leitor,
        EfeitosDaTrocaDeVigencia efeitos,
        IAutorizacaoUnitOfWork unitOfWork,
        ICatalogoDePermissoesConsultavel catalogo,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(leitor);
        ArgumentNullException.ThrowIfNull(efeitos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(clock);

        _perfis = perfis;
        _leitor = leitor;
        _efeitos = efeitos;
        _unitOfWork = unitOfWork;
        _catalogo = catalogo;
        _clock = clock;
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

        Perfil perfil = await _perfis.ObterParaEditarAsync(perfilId.Value, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"O perfil {perfilId} do documento {resultado.ArquivoId} não existe.");

        IReadOnlyList<EnviarEmail>? mensagens = await AplicarAsync(perfil, resultado, cancellationToken).ConfigureAwait(false);
        if (mensagens is null)
        {
            return RegistroDoResultadoDaVersao.Sem(DesfechoDoResultadoDaVersao.Ignorado);
        }

        await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return new RegistroDoResultadoDaVersao(DesfechoDoResultadoDaVersao.Aplicado, mensagens);
    }

    // Nulo quando o resultado foi ignorado; do contrário, os avisos dos efeitos, que podem ser nenhum.
    private async Task<IReadOnlyList<EnviarEmail>?> AplicarAsync(
        Perfil perfil, ResultadoDaVerificacaoDeArquivo resultado, CancellationToken cancellationToken)
    {
        if (resultado.Veredicto != VeredictoDaVerificacao.Verificado)
        {
            return perfil.RegistrarDocumentoRecusado(resultado.ArquivoId, _clock.GetUtcNow()) ? [] : null;
        }

        string nomeDaVersao = perfil.Versoes.First(v => v.Documentos.Any(d => d.Id == resultado.ArquivoId)).Nome;
        ContextoDaVersao contexto = await _leitor.LerAsync(perfil.Id, nomeDaVersao, cancellationToken).ConfigureAwait(false);
        ResultadoDoDocumentoVerificado verificado = perfil.RegistrarDocumentoVerificado(
            resultado.ArquivoId, resultado.HashSha256!, _clock.GetUtcNow(), contexto, _catalogo);

        return verificado.Desfecho == DesfechoDoDocumentoVerificado.Ignorado
            ? null
            : await EfeitosDaTrocaAsync(perfil, verificado.Troca, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<EnviarEmail>> EfeitosDaTrocaAsync(
        Perfil perfil, TrocaDeVigencia? troca, CancellationToken cancellationToken) =>
        troca is { Anterior: not null }
            ? await _efeitos.AplicarAsync(perfil, troca, cancellationToken).ConfigureAwait(false)
            : [];
}
