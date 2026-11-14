namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O processo conforme que os testes dos handlers que congelam publicam, e o ato que acompanha a publicação.
/// </summary>
internal static class PublicacaoDeTeste
{
    public static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static ProcessoSeletivo NovoProcessoConforme() =>
        ProcessoSeletivoConformeBuilder.Criar("PS 2026 — SiSU");

    public static DadosDoAto NovoAto() => new(
        Orgao: "CEPS",
        Serie: "EDITAL",
        Ano: 2026,
        DataDoAto: new DateOnly(2026, 1, 1),
        Assinante: "Diretor do CEPS",
        TipoAtoCodigo: "EDITAL_ABERTURA");

    /// <summary>Processo publicado (para os cenários de Retificar e FecharRetificacao), com a mesma configuração conforme.</summary>
    public static (ProcessoSeletivo Processo, VersaoConfiguracao VersaoAtual) ProcessoPublicado(byte[]? envelopeCongelado = null)
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        return (processo, Publicar(processo, envelopeCongelado));
    }

    public static VersaoConfiguracao Publicar(ProcessoSeletivo processo, byte[]? envelopeCongelado = null)
    {
        Result<VersaoConfiguracao> versao = processo.Publicar(
            DadosDoEdital(), envelopeCongelado ?? EnvelopeCongeladoDeTeste.SemAnexos(), "1.1", "canonical-json/sha256@v1",
            ProcessoSeletivoConformeBuilder.HashFixo, "user-sub-1", new RelogioFixo(Agora),
            ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        versao.IsSuccess.Should().BeTrue(versao.Error?.Message);
        processo.DequeueDomainEvents();

        return versao.Value!;
    }

    /// <summary>Abre a sessão editorial de retificação sobre a versão vigente, sem nenhuma outra versão publicada.</summary>
    public static void AbrirRetificacao(ProcessoSeletivo processo, VersaoConfiguracao versaoAtual)
    {
        Result<RascunhoRetificacao> rascunho = processo.AbrirRetificacao(
            "Correção do prazo", versaoAtual, identificadorDaVersaoBase: null,
            VersoesPublicadasDeTeste.SoAVigente(processo), "user-sub-1", Agora);
        rascunho.IsSuccess.Should().BeTrue(rascunho.Error?.Message);
        processo.DequeueDomainEvents();
    }

    public static DadosEdital DadosDoEdital() =>
        DadosEdital.Criar(
            "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;
}
