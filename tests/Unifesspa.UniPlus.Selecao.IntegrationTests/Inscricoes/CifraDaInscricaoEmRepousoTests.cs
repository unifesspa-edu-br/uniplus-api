namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDaInscricaoGravada;

/// <summary>
/// A linha crua da inscrição não guarda o CPF, o número de documento nem o dado sensível em texto claro (ADR-0121):
/// o documento e o sensível ficam cifrados, cada um com a chave da categoria dele, e o titular os lê de volta.
/// </summary>
[Trait("Category", "Integration")]
[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class CifraDaInscricaoEmRepousoTests
{
    private const string RespostaDoDocumento = """
        SELECT r.valor_texto, r.valor_cifrado
        FROM selecao.inscricoes i JOIN selecao.respostas_da_inscricao r ON r.inscricao_id = i.id
        WHERE i.id = @id AND r.codigo_do_dado = 'RG_NUMERO'
        """;

    private const string RespostaDoCpf = """
        SELECT r.valor_texto, r.valor_cifrado,
               (SELECT count(*) FROM information_schema.columns
                WHERE table_schema = 'selecao' AND table_name = 'inscricoes' AND column_name LIKE 'cpf%') AS colunas_do_cpf
        FROM selecao.respostas_da_inscricao r
        WHERE r.inscricao_id = @id AND r.codigo_do_dado = 'CPF'
        """;

    private const string RespostasDaInscricao =
        "SELECT codigo_do_dado, valor_codigo, valor_cifrado FROM selecao.respostas_da_inscricao WHERE inscricao_id = @id";

    private const string ComprovanteCru =
        "SELECT respostas::text, sensiveis_cifrados FROM selecao.comprovantes_inscricao WHERE inscricao_id = @id";

    private readonly CenarioDaInscricaoGravada _cenario;

    public CifraDaInscricaoEmRepousoTests(InscricaoDbFixture fixture) => _cenario = new CenarioDaInscricaoGravada(fixture);

    [Fact(DisplayName = "A linha crua não contém o CPF nem o número de documento em texto claro")]
    public async Task ColunaCifrada_NuncaContemCpfNemDocumentoEmTextoClaro()
    {
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        original.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(("RG_NUMERO", "12.868.927-4")), SemGrupos(), []), Admitidas, PrecondicaoDe(original));
        await _cenario.GravarAsync(original);

        await ConferirQueOGravadoDoDocumentoEstaCifradoAsync(original.Id);
        await ConferirQueOGravadoDoCpfEstaCifradoAsync(original.Id);
    }

    [Fact(DisplayName = "O identificador cifrado de valor numérico é revelado como o texto do número, sem falhar")]
    public async Task IdentificadorNumerico_ERevelado()
    {
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        original.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(OfertaDoProcesso, null), Respostas(("RG_NUMERO", "12.868.927-4")), SemGrupos(), [AceiteDoTermoDeVeracidade()]), Admitidas, PrecondicaoDe(original));
        await _cenario.GravarAsync(original);

        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        InscricaoRepository leitura = _cenario.Repositorio(contexto);
        RespostaIdentificadora rg = (await leitura.LocalizarIdentificadorAsync(original.Id, "RG_NUMERO", null, null))!;
        RespostaDaInscricaoRecord linha = await contexto.RespostasDaInscricao.SingleAsync(r => r.Id == rg.RespostaId);
        linha.ValorCifrado = await _cenario.Encryption.EncryptAsync(
            SelecaoEncryptionKeys.IdentificadoresAesGcm, Encoding.UTF8.GetBytes("1286892"), CancellationToken.None);
        await contexto.SaveChangesAsync();

        (await leitura.LerIdentificadorAsync(rg.RespostaId)).Should().Be("1286892");
    }

    [Fact(DisplayName = "A identidade de gênero e a orientação sexual ficam cifradas na resposta e no comprovante, o sexo civil em claro, e o titular lê os valores de volta")]
    public async Task DadoSensivel_FicaCifradoEmRepousoEVoltaAoTitular()
    {
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        ComprovanteInscricao comprovante = Enviar(original, "000555", new ConteudoDoEnvio
        {
            Respostas = Respostas(("SEXO_CIVIL", "FEMININO"), ("IDENTIDADE_GENERO", "PESSOA_NAO_BINARIA"), ("ORIENTACAO_SEXUAL", "BISSEXUAL")),
        }).Value!;
        await _cenario.GravarAsync(original, comprovante);

        await ConferirQueAsRespostasSensiveisEstaoCifradasAsync(original.Id);
        await ConferirQueOComprovanteGuardaOsSensiveisCifradosAsync(original.Id);
        await ConferirQueOTitularLeOsValoresDeVoltaAsync(original.Id);
    }

    private async Task ConferirQueOGravadoDoDocumentoEstaCifradoAsync(Guid inscricaoId)
    {
        List<(object Texto, byte[] Cifrado)> linhas = await _cenario.ConsultarAsync(
            RespostaDoDocumento, inscricaoId, static l => (l["valor_texto"], (byte[])l["valor_cifrado"]));

        linhas.Should().NotBeEmpty();
        linhas[0].Texto.Should().Be(DBNull.Value, "o número de documento nunca fica na coluna de texto em claro");
        Encoding.UTF8.GetString(linhas[0].Cifrado).Should().NotContain("12.868.927-4");
    }

    private async Task ConferirQueOGravadoDoCpfEstaCifradoAsync(Guid inscricaoId)
    {
        List<(object Texto, byte[] Cifrado, object ColunasDoCpf)> linhas = await _cenario.ConsultarAsync(
            RespostaDoCpf, inscricaoId, static l => (l["valor_texto"], (byte[])l["valor_cifrado"], l["colunas_do_cpf"]));

        linhas.Should().NotBeEmpty("o CPF da inscrição é a resposta do fato CPF");
        linhas[0].Texto.Should().Be(DBNull.Value, "o CPF nunca fica em texto claro");
        Encoding.UTF8.GetString(linhas[0].Cifrado).Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor);
        Convert.ToInt32(linhas[0].ColunasDoCpf, System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(1, "só o índice cego do CPF fica na inscrição");
    }

    private async Task ConferirQueAsRespostasSensiveisEstaoCifradasAsync(Guid inscricaoId)
    {
        Dictionary<string, (object? Codigo, byte[]? Cifrado)> linhas = (await _cenario.ConsultarAsync(RespostasDaInscricao, inscricaoId, LinhaDaResposta))
            .ToDictionary(static l => l.Fato, static l => (l.Codigo, l.Cifrado));

        linhas["SEXO_CIVIL"].Should().Be(("FEMININO", (byte[]?)null), "o sexo civil não é dado sensível: fica em claro");
        foreach ((string fato, string valor) in new[] { ("IDENTIDADE_GENERO", "PESSOA_NAO_BINARIA"), ("ORIENTACAO_SEXUAL", "BISSEXUAL") })
        {
            await ConferirQueOSensivelEstaCifradoComASuaChaveAsync(fato, valor, linhas[fato].Codigo, linhas[fato].Cifrado!);
        }
    }

    private async Task ConferirQueOSensivelEstaCifradoComASuaChaveAsync(string fato, string valor, object? codigo, byte[] cifrado)
    {
        codigo.Should().BeNull($"{fato} nunca fica na coluna em claro");
        Encoding.UTF8.GetString(cifrado).Should().NotContain(valor);
        Encoding.UTF8.GetString(await _cenario.Encryption.DecryptAsync(SelecaoEncryptionKeys.SensiveisAesGcm, cifrado))
            .Should().Contain(valor, "a chave da categoria dos sensíveis decifra o valor");
        Func<Task> comChaveDeDocumento = async () => await _cenario.Encryption.DecryptAsync(SelecaoEncryptionKeys.IdentificadoresAesGcm, cifrado);
        await comChaveDeDocumento.Should().ThrowAsync<Exception>($"{fato} não usa a chave dos números de documento");
    }

    private async Task ConferirQueOComprovanteGuardaOsSensiveisCifradosAsync(Guid inscricaoId)
    {
        List<(string Respostas, byte[] SensiveisCifrados)> linhas = await _cenario.ConsultarAsync(
            ComprovanteCru, inscricaoId, static l => ((string)l["respostas"], (byte[])l["sensiveis_cifrados"]));

        linhas.Should().NotBeEmpty();
        linhas[0].Respostas.Should().Contain("FEMININO").And.NotContain("PESSOA_NAO_BINARIA").And.NotContain("BISSEXUAL");
        Encoding.UTF8.GetString(linhas[0].SensiveisCifrados).Should().NotContain("PESSOA_NAO_BINARIA").And.NotContain("BISSEXUAL");
    }

    private async Task ConferirQueOTitularLeOsValoresDeVoltaAsync(Guid inscricaoId)
    {
        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        InscricaoRepository leitura = _cenario.Repositorio(contexto);

        ComprovanteInscricao lido = (await _cenario.Comprovantes(contexto).ObterComprovanteAsync(inscricaoId, 1))!.Comprovante;
        Inscricao relida = (await leitura.ObterPorIdAsync(inscricaoId))!;

        lido.TodasAsRespostas()["IDENTIDADE_GENERO"].GetString().Should().Be("PESSOA_NAO_BINARIA");
        lido.TodasAsRespostas()["ORIENTACAO_SEXUAL"].GetString().Should().Be("BISSEXUAL");
        lido.TodasAsRespostas()["SEXO_CIVIL"].GetString().Should().Be("FEMININO");
        relida.Formulario.Preenchimento.Respostas["ORIENTACAO_SEXUAL"].GetString().Should().Be("BISSEXUAL");
        relida.Formulario.DefinicoesDasRespostas["ORIENTACAO_SEXUAL"].Should().Match<DefinicaoDaResposta>(static d => d.CifraIndividual && !d.Identificador);
    }

    private static (string Fato, object? Codigo, byte[]? Cifrado) LinhaDaResposta(NpgsqlDataReader leitor) =>
        ((string)leitor["codigo_do_dado"], leitor["valor_codigo"] is DBNull ? null : leitor["valor_codigo"], leitor["valor_cifrado"] as byte[]);
}
