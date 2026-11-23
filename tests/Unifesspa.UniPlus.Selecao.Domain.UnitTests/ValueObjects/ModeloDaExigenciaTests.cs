namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ModeloDaExigenciaTests
{
    private static readonly TimeProvider Relogio = new RelogioFixo(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void DoCitado_ModeloConfirmadoDoDono_CopiaOsDados()
    {
        ModeloDeDocumento modelo = ModeloPendente();
        string hash = new('a', 64);
        modelo.Confirmar(10, hash, Relogio).IsSuccess.Should().BeTrue();

        Result<ModeloDaExigencia> resultado = ModeloDaExigencia.DoCitado(modelo.Id, new Dictionary<Guid, ModeloDeDocumento> { [modelo.Id] = modelo });

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().Be(new ModeloDaExigencia(modelo.Id, modelo.NomeArquivo, modelo.Formato, hash));
    }

    [Fact]
    public void DoCitado_ModeloAusenteOuDeOutroDono_RecusaSemIdentificador()
    {
        ModeloDeDocumento modeloDeOutroDono = ModeloPendente();
        Dictionary<Guid, ModeloDeDocumento> modelosDoDono = [];

        Result<ModeloDaExigencia> resultado = ModeloDaExigencia.DoCitado(modeloDeOutroDono.Id, modelosDoDono);

        resultado.Error!.Code.Should().Be(ModeloDaExigenciaErrorCodes.ModeloNaoEncontrado);
        resultado.Error.Message.Should().Be("O modelo de documento citado não foi enviado para este processo.");
        resultado.Error.Message.Should().NotContain(modeloDeOutroDono.Id.ToString());
    }

    [Fact]
    public void DoCitado_ModeloPendente_Recusa()
    {
        ModeloDeDocumento modelo = ModeloPendente();

        Result<ModeloDaExigencia> resultado = ModeloDaExigencia.DoCitado(modelo.Id, new Dictionary<Guid, ModeloDeDocumento> { [modelo.Id] = modelo });

        resultado.Error!.Code.Should().Be(ModeloDeDocumentoErrorCodes.NaoConfirmado);
    }

    private static ModeloDeDocumento ModeloPendente()
    {
        Result<ModeloDeDocumento> resultado = ModeloDeDocumento.IniciarPendente(Guid.NewGuid(), "Declaração", "DOCX", Relogio, TimeSpan.FromMinutes(15));
        resultado.IsSuccess.Should().BeTrue();
        return resultado.Value!;
    }
}
