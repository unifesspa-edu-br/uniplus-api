namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaPublicacaoDoProcessoSeletivo;

internal static class ApoioDaSessaoEditorialDoProcessoSeletivo
{
    internal static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    internal static ProcessoSeletivo NovoProcessoPublicado(out VersaoConfiguracao versao)
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.1", "canonical-json/sha256@v1", HashFixo, "user-conta-123",
            new RelogioFixo(Agora), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        versao = publicacao.Value!;
        processo.DequeueDomainEvents();
        return processo;
    }

    internal static Result<RascunhoRetificacao> AbrirSessao(
        ProcessoSeletivo processo, VersaoConfiguracao versao, string motivo = "Correção do prazo", string conta = "user-conta-1") =>
        processo.AbrirRetificacao(
            motivo, versao, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(processo), conta, Agora);

    internal static ProcessoSeletivo ComSessaoAberta(out RascunhoRetificacao rascunho)
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);
        rascunho = AbrirSessao(processo, versao).Value!;
        return processo;
    }

    internal static void ForcarStatus(ProcessoSeletivo processo, StatusProcesso status) =>
        typeof(ProcessoSeletivo)
            .GetProperty(nameof(ProcessoSeletivo.Status))!
            .SetValue(processo, status);
}
