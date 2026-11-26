namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O dado decifrado que não é um documento JSON legível é recusado com mensagem fixa: nem o texto decifrado nem a exceção da
/// biblioteca saem na mensagem, porque o texto decifrado é dado pessoal.
/// </summary>
public sealed class LeitoresDeDadoDecifradoInvalidoTests
{
    private const string Marcador = "valor-gravado-ficticio-7f3a";

    [Fact(DisplayName = "O CPF decifrado que não é JSON é recusado com mensagem fixa, sem o texto decifrado")]
    public async Task LerAsync_CpfDecifradoQueNaoEJson_RecusaComMensagemFixa()
    {
        Func<Task> leitura = () => CpfDaInscricao.LerAsync(RespostasDoCpf(), new CifraDeTeste(Marcador), CancellationToken.None);

        await leitura.DeveRecusarComoDocumentoInvalidoAsync(nameof(Cpf), Marcador);
    }

    [Fact(DisplayName = "O CPF decifrado como texto JSON é lido")]
    public async Task LerAsync_CpfDecifradoComoTextoJson_DevolveOCpf()
    {
        string cpf = PersonasDeTeste.KevinCarvalho.Valor;

        Dictionary<Guid, string> cpfs = await CpfDaInscricao.LerAsync(
            RespostasDoCpf(), new CifraDeTeste(JsonSerializer.Serialize(cpf)), CancellationToken.None);

        cpfs.Values.Should().ContainSingle().Which.Should().Be(cpf);
    }

    [Fact(DisplayName = "As respostas sensíveis decifradas que não são JSON são recusadas com mensagem fixa, sem o texto decifrado")]
    public async Task RestaurarAsync_SensiveisQueNaoSaoJson_RecusaComMensagemFixa()
    {
        SensiveisDoComprovante sensiveis = new(new CifraDeTeste(Marcador), new IndiceCegoDeTeste());
        Func<Task> leitura = () => sensiveis.RestaurarAsync(ComprovanteComVinculo(IndiceCegoDeTeste.Vinculo), CancellationToken.None);

        await leitura.DeveRecusarComoDocumentoInvalidoAsync(nameof(ComprovanteInscricao.RespostasSensiveis), Marcador);
    }

    [Fact(DisplayName = "As respostas sensíveis decifradas com o vínculo incoerente são recusadas antes de ler o conteúdo")]
    public async Task RestaurarAsync_VinculoIncoerente_RecusaAntesDeLerOConteudo()
    {
        SensiveisDoComprovante sensiveis = new(new CifraDeTeste(Marcador), new IndiceCegoDeTeste());
        Func<Task> leitura = () => sensiveis.RestaurarAsync(ComprovanteComVinculo([9, 9, 9, 9]), CancellationToken.None);

        InvalidOperationException erro = (await leitura.Should().ThrowAsync<InvalidOperationException>()).Which;

        erro.Message.Should().Contain("o vínculo não confere").And.NotContain(Marcador);
    }

    [Fact(DisplayName = "O valor cifrado da resposta que não é JSON é recusado com mensagem fixa, sem o texto decifrado")]
    public async Task DecifrarAsync_ValorQueNaoEJson_RecusaComMensagemFixa()
    {
        ValorGravadoDaResposta valor = new(new CifraDeTeste(Marcador));
        Func<Task> leitura = () => valor.DecifrarAsync("chave-ficticia", [1, 2, 3], CancellationToken.None);

        await leitura.DeveRecusarComoDocumentoInvalidoAsync(nameof(RespostaDaInscricaoRecord.ValorCifrado), Marcador);
    }

    [Fact(DisplayName = "O valor cifrado da resposta que é JSON válido é lido")]
    public async Task DecifrarAsync_ValorJsonValido_DevolveOElemento()
    {
        ValorGravadoDaResposta valor = new(new CifraDeTeste("\"valor-valido\""));

        JsonElement lido = await valor.DecifrarAsync("chave-ficticia", [1, 2, 3], CancellationToken.None);

        lido.GetString().Should().Be("valor-valido");
    }

    [Fact(DisplayName = "A data da resposta fora do formato é recusada na gravação sem repetir o texto recebido")]
    public async Task GravarAsync_DataForaDoFormato_RecusaSemRepetirOTexto()
    {
        ValorGravadoDaResposta valor = new(new CifraDeTeste("{}"));
        RespostaDaInscricaoRecord linha = new() { FatoCodigo = "DATA_FICTICIA", Tipo = TipoDoValorDeResposta.Data };
        DefinicaoDaResposta definicao = new(TipoDoValorDeResposta.Data, false, false);
        JsonElement texto = JsonSerializer.SerializeToElement(Marcador);

        Func<Task> gravacao = () => valor.GravarAsync(linha, texto, definicao, null, CancellationToken.None);

        InvalidOperationException erro = (await gravacao.Should().ThrowAsync<InvalidOperationException>()).Which;
        erro.Message.Should().Be("A resposta do tipo data não está no formato de dia esperado.");
        erro.InnerException.Should().BeNull();
    }

    [Fact(DisplayName = "A data da resposta no formato do dia é gravada na coluna")]
    public async Task GravarAsync_DataNoFormato_GravaAColuna()
    {
        ValorGravadoDaResposta valor = new(new CifraDeTeste("{}"));
        RespostaDaInscricaoRecord linha = new() { FatoCodigo = "DATA_FICTICIA", Tipo = TipoDoValorDeResposta.Data };
        DefinicaoDaResposta definicao = new(TipoDoValorDeResposta.Data, false, false);

        await valor.GravarAsync(linha, JsonSerializer.SerializeToElement("2026-10-10"), definicao, null, CancellationToken.None);

        linha.ValorData.Should().Be(new DateOnly(2026, 10, 10));
    }

    private static ConsultaAssincronaDeTeste<RespostaDaInscricaoRecord> RespostasDoCpf() =>
        new([
            new RespostaDaInscricaoRecord
            {
                Id = Guid.NewGuid(),
                InscricaoId = Guid.NewGuid(),
                FatoCodigo = ConjuntoBasicoDaInscricao.Cpf,
                Origem = OrigemDaResposta.Informada,
                ValorCifrado = [1, 2, 3],
            },
        ]);

    private static ComprovanteInscricao ComprovanteComVinculo(byte[] vinculo)
    {
        ComprovanteInscricao comprovante = ComprovanteInscricao.Emitir(
            Guid.NewGuid(),
            "000001",
            new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero),
            null,
            new Dictionary<string, JsonElement>(),
            new Dictionary<string, JsonElement>(),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(),
            [],
            []);
        comprovante.GuardarSensiveisCifrados([1, 2, 3], vinculo);
        return comprovante;
    }
}
