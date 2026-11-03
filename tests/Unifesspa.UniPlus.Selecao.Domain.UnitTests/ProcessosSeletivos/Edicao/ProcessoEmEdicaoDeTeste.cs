namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>O processo conforme já publicado, sem sessão ou com a retificação aberta, para os testes da edição.</summary>
internal static class ProcessoEmEdicaoDeTeste
{
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    internal static ProcessoSeletivo PublicadoSemSessao(out VersaoConfiguracao versao)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            DadosEdital.Criar(
                "001/2026",
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
                new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
                Guid.CreateVersion7()).Value!,
            Encoding.UTF8.GetBytes(new JsonObject { ["status"] = "ok" }.ToJsonString()), "1.1", "canonical-json/sha256@v1",
            new string('a', 64), "user-sub-123", new RelogioFixo(Agora),
            ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        processo.DequeueDomainEvents();
        versao = publicacao.Value!;
        return processo;
    }

    internal static ProcessoSeletivo EmSessao()
    {
        ProcessoSeletivo processo = PublicadoSemSessao(out VersaoConfiguracao versao);
        processo.AbrirRetificacao(
            "Correção do prazo", versao, identificadorDaVersaoBase: null,
            VersoesPublicadasDeTeste.SoAVigente(processo), "user-sub-1", Agora).IsSuccess.Should().BeTrue();
        return processo;
    }
}
