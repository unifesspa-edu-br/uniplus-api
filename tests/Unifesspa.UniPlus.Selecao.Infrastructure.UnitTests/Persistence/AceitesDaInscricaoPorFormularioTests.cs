namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O aceite do termo na inscrição guarda o código do formulário em que o termo é exigido, de modo que o mesmo termo
/// aceito em dois formulários não se confunde. Não abre conexão: só a tradução entre a inscrição e as linhas.
/// </summary>
public sealed class AceitesDaInscricaoPorFormularioTests
{
    private static readonly AceiteDeTermo[] AceitesDoTeste =
    [
        new("INSCRICAO", "CIENCIA_DO_EDITAL", new Guid("11111111-1111-4111-8111-111111111111"), "hash-da-inscricao"),
        new("HABILITACAO", "CIENCIA_DO_EDITAL", new Guid("22222222-2222-4222-8222-222222222222"), "hash-da-habilitacao"),
    ];

    private readonly TraducaoDaInscricao _traducao = new(new ValorGravadoDaResposta(new CifraQueNaoSeUsa()));

    [Fact(DisplayName = "Cada aceite é gravado com o código do formulário e na ordem em que foi dado")]
    public async Task LinhasAsync_Aceites_GravamOFormularioNaOrdemDada()
    {
        Inscricao inscricao = InscricaoComAceites(AceitesDoTeste);

        LinhasDaInscricao linhas = await _traducao.LinhasAsync(inscricao, CancellationToken.None);

        linhas.Aceites.OrderBy(static a => a.Ordem)
            .Select(static a => (a.Ordem, a.FormularioCodigo, a.TermoCodigo, a.HashVersao))
            .Should().Equal(
                (0, "INSCRICAO", "CIENCIA_DO_EDITAL", "hash-da-inscricao"),
                (1, "HABILITACAO", "CIENCIA_DO_EDITAL", "hash-da-habilitacao"));
    }

    [Fact(DisplayName = "A inscrição lida devolve cada aceite com o formulário gravado, na ordem gravada")]
    public async Task InscricaoAsync_LinhasDeAceite_DevolvemOFormularioDeCadaAceite()
    {
        Inscricao gravada = InscricaoComAceites(AceitesDoTeste);
        LinhasDaInscricao linhas = await _traducao.LinhasAsync(gravada, CancellationToken.None);
        linhas.Aceites.Reverse();
        InscricaoRecord registro = new() { Id = gravada.Id, CreatedAt = DateTimeOffset.UnixEpoch };
        TraducaoDaInscricao.PreencherRegistro(registro, gravada, new byte[32]);

        Inscricao lida = await _traducao.InscricaoAsync(registro, linhas, CancellationToken.None);

        lida.Formulario.Preenchimento.Aceites.Should().Equal(AceitesDoTeste);
    }

    private static Inscricao InscricaoComAceites(IReadOnlyList<AceiteDeTermo> aceites)
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        FormularioDaInscricao formulario = FormularioDaInscricao.Reidratar(
            Guid.CreateVersion7(), PreenchimentoComCpf(cpf, aceites), new Dictionary<string, JsonElement>(StringComparer.Ordinal), DefinicaoDoCpf());

        return Inscricao.Reidratar(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            TitularDaInscricao.Reidratar(cpf, "conta-ficticia", "emissor-ficticio"),
            EstadoDaInscricao.Reidratar(SituacaoInscricao.Rascunho, null, null),
            formulario,
            1,
            new DatasDeGravacaoDaInscricao(DateTimeOffset.UnixEpoch, null));
    }

    private static PreenchimentoDoCandidato PreenchimentoComCpf(Cpf cpf, IReadOnlyList<AceiteDeTermo> aceites) =>
        PreenchimentoDoCandidato.Criar(
            OpcoesDeCursoDaInscricao.Reidratar(null, null),
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { [ConjuntoBasicoDaInscricao.Cpf] = JsonSerializer.SerializeToElement(cpf.Valor) },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal),
            aceites);

    private static Dictionary<string, DefinicaoDaResposta> DefinicaoDoCpf() => new(StringComparer.Ordinal)
    {
        [ConjuntoBasicoDaInscricao.Cpf] = new DefinicaoDaResposta(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false),
    };

    private sealed class CifraQueNaoSeUsa : IUniPlusEncryptionService
    {
        public Task<byte[]> EncryptAsync(string keyName, byte[] plaintext, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("A inscrição do teste não tem resposta cifrada.");

        public Task<byte[]> DecryptAsync(string keyName, byte[] ciphertext, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("A inscrição do teste não tem resposta cifrada.");
    }
}
