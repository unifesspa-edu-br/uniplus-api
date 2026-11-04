namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using Queries.ProcessosSeletivos;

/// <summary>
/// Grava a divulgação do certame: cria a linha na primeira versão divulgada, avança nas seguintes e
/// salva, classificando a violação do identificador legível único.
/// </summary>
internal sealed class GravacaoDaDivulgacao
{
    public GravacaoDaDivulgacao(ICertameDivulgadoRepository certames, ISelecaoUnitOfWork unitOfWork, TimeProvider relogio)
    {
        Certames = certames;
        UnitOfWork = unitOfWork;
        Relogio = relogio;
    }

    private ICertameDivulgadoRepository Certames { get; }

    private ISelecaoUnitOfWork UnitOfWork { get; }

    private TimeProvider Relogio { get; }

    /// <summary>
    /// Confere, sem rastrear a linha, que o processo já tem divulgação para receber os documentos do ato:
    /// a linha para mutação só é lida depois, para não segurar a versão dela durante cópias demoradas.
    /// </summary>
    internal async Task ExigirDivulgacaoAsync(AtoDoProcesso ato, CancellationToken cancellationToken)
    {
        if (await Certames.ObterParaLeituraAsync(ato.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false) is null)
        {
            throw new CertameAindaNaoDivulgadoException(
                $"O processo {ato.ProcessoSeletivoId} ainda não tem divulgação para receber os documentos do ato {ato.Id}.");
        }
    }

    /// <summary>
    /// Registra na divulgação do processo o edital do ato que não cria versão. Registrado só depois da
    /// cópia: o endereço divulgado nunca aponta para arquivo ausente.
    /// </summary>
    internal async Task RegistrarEditalDoAtoAsync(AtoDoProcesso ato, EditalDivulgado edital, CancellationToken cancellationToken)
    {
        CertameDivulgado divulgado = await Certames
            .ObterParaMutacaoAsync(ato.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new CertameAindaNaoDivulgadoException(
                $"O processo {ato.ProcessoSeletivoId} perdeu a divulgação durante a cópia dos documentos do ato {ato.Id}.");

        if (divulgado.RegistrarEdital(edital))
        {
            await UnitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task RegistrarAsync(
        VersaoConfiguracao versao,
        CertamePublicadoDto certame,
        EditalDivulgado edital,
        CancellationToken cancellationToken)
    {
        string documento = JsonSerializer.Serialize(certame, ProjecaoDoCertamePublicado.OpcoesDoDocumento);
        DateTimeOffset agora = Relogio.GetUtcNow();

        // Da MESMA projeção que produziu o documento: é o que impede consulta e resposta divergirem.
        FacetasDoCertameDivulgado facetas = new(
            certame.IdentificadorLegivel,
            certame.Nome,
            certame.Periodo.Numero,
            certame.ModalidadesOfertadas,
            certame.Periodo.Inicio,
            certame.Periodo.Fim);

        CertameDivulgado? divulgado = await Certames
            .ObterParaMutacaoAsync(versao.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        bool alterou = divulgado is null
            ? await AdicionarAsync(versao, edital, facetas, documento, agora, cancellationToken).ConfigureAwait(false)
            : Atualizar(divulgado, versao, edital, facetas, documento, agora);

        if (alterou)
        {
            await SalvarAsync(versao, certame, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> AdicionarAsync(
        VersaoConfiguracao versao,
        EditalDivulgado edital,
        FacetasDoCertameDivulgado facetas,
        string documento,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        CertameDivulgado nova = CertameDivulgado.Criar(
            versao.ProcessoSeletivoId,
            versao.NumeroVersao,
            versao.AtoCriadorId,
            versao.HashConfiguracao,
            ProjecaoDoCertamePublicado.Versao,
            facetas,
            documento,
            agora);
        nova.RegistrarEdital(edital);

        await Certames.AdicionarAsync(nova, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static bool Atualizar(
        CertameDivulgado divulgado,
        VersaoConfiguracao versao,
        EditalDivulgado edital,
        FacetasDoCertameDivulgado facetas,
        string documento,
        DateTimeOffset agora)
    {
        bool avancou = divulgado.TentarAvancar(
            versao.NumeroVersao,
            versao.AtoCriadorId,
            versao.HashConfiguracao,
            ProjecaoDoCertamePublicado.Versao,
            facetas,
            documento,
            agora);

        // O edital registra-se também quando a versão não avança: o ato atrasado de uma versão anterior
        // não desfaz a vigente, mas o seu edital já está no acervo e a linha do tempo precisa dele.
        // Reentrega do que já está registrado não muda nada.
        bool registrou = divulgado.RegistrarEdital(edital);
        return avancou || registrou;
    }

    private async Task SalvarAsync(VersaoConfiguracao versao, CertamePublicadoDto certame, CancellationToken cancellationToken)
    {
        try
        {
            await UnitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception falha) when (IdentificadorLegivelJaDivulgadoException.EhViolacaoDoIndice(falha))
        {
            UnitOfWork.DescartarAlteracoesNaoSalvas();

            CertameDivulgado? dono = await Certames
                .ObterParaLeituraPorIdentificadorAsync(certame.IdentificadorLegivel, cancellationToken)
                .ConfigureAwait(false);

            // Linha do próprio processo (entregas de versões dele correndo juntas) ou já removida: é
            // corrida, que a reentrega resolve — a falha original segue para a política transiente.
            if (IdentificadorLegivelJaDivulgadoException.Classificar(
                falha, versao.ProcessoSeletivoId, dono?.Id, certame.IdentificadorLegivel) is not { } conflito)
            {
                throw;
            }

            throw conflito;
        }
    }
}
