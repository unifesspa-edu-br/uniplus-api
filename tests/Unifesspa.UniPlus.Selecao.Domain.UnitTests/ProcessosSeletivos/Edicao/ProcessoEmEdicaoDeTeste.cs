namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>O processo conforme, em rascunho, publicado ou encerrado, para os testes da edição.</summary>
internal static class ProcessoEmEdicaoDeTeste
{
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <param name="prepararAntesDePublicar">Estado que a configuração publicada já precisa ter, aplicado no rascunho.</param>
    internal static ProcessoSeletivo Publicado(out VersaoConfiguracao versao, Action<ProcessoSeletivo>? prepararAntesDePublicar = null)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        prepararAntesDePublicar?.Invoke(processo);
        Result<VersaoConfiguracao> publicacao = processo.Publicar(new CenarioDeCongelamento { Dados = DadosDoEdital(), Documento = DocumentoDoEdital(), Relogio = new RelogioFixo(Agora) });
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        processo.DequeueDomainEvents();
        versao = publicacao.Value!;
        return processo;
    }

    internal static ProcessoSeletivo Encerrado(Action<ProcessoSeletivo>? prepararAntesDePublicar = null)
    {
        ProcessoSeletivo processo = Publicado(out _, prepararAntesDePublicar);
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Status))!.SetValue(processo, StatusProcesso.Encerrado);
        return processo;
    }

    private static DadosEdital DadosDoEdital() => DadosEdital.Criar(
        "001/2026",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
        Guid.CreateVersion7()).Value!;

    private static DocumentoCongelado DocumentoDoEdital() =>
        new(Encoding.UTF8.GetBytes(new JsonObject { ["status"] = "ok" }.ToJsonString()), "1.1", "canonical-json/sha256@v1", new string('a', 64));
}
