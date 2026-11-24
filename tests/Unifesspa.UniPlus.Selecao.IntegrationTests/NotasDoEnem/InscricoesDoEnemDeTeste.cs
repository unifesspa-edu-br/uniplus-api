namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Semeia, direto nas tabelas, o que as notas do ENEM leem de uma inscrição: a resposta do CPF cifrada, o
/// índice cego, a situação e a resposta da edição escolhida. Evita montar o fluxo inteiro da
/// inscrição para provar um cruzamento.
/// </summary>
internal static class InscricoesDoEnemDeTeste
{
    public static async Task<ProcessoSeletivo> NovoProcessoAsync(UniPlusDbContext context)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2027 — notas do ENEM", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        context.ProcessosSeletivos.Add(processo);
        await context.SaveChangesAsync();
        return processo;
    }

    /// <summary>
    /// Grava uma versão divulgada mínima do processo, só o que a chave estrangeira do lote exige: o
    /// conteúdo congelado não é lido por quem a usa.
    /// </summary>
    public static async Task<Guid> NovaVersaoAsync(InscricaoDbFixture fixture, Guid processoId)
    {
        await using UniPlusDbContext context = fixture.CreateDbContext();
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processoId, new DocumentoCongelado(Encoding.UTF8.GetBytes("{}"), "1", "sha256@v1", new string('a', 64)),
            Guid.CreateVersion7(), "integration-test-user", DateTimeOffset.UtcNow);
        context.VersoesConfiguracao.Add(versao);
        await context.SaveChangesAsync();
        return versao.Id;
    }

    public static async Task<Guid> NovaInscricaoAsync(InscricaoDbFixture fixture, DadosDaInscricaoDoEnem dados)
    {
        await using UniPlusDbContext context = fixture.CreateDbContext();
        return await NovaInscricaoAsync(context, fixture.Encryption, fixture.BlindIndex, dados);
    }

    public static async Task<Guid> NovaInscricaoAsync(
        UniPlusDbContext context, IUniPlusEncryptionService encryption,
        IUniPlusBlindIndexService blindIndex, DadosDaInscricaoDoEnem dados)
    {
        byte[] cifrado = await encryption.EncryptAsync(
            SelecaoEncryptionKeys.IdentificadoresAesGcm, JsonSerializer.SerializeToUtf8Bytes(dados.Pessoa.Cpf));
        byte[] indice = await new CalculadorDoIndiceCegoDoCpf(blindIndex).ComputarAsync(dados.Pessoa.Cpf);
        Guid id = Guid.CreateVersion7();
        context.Inscricoes.Add(Inscricao(id, indice, dados));
        context.RespostasDaInscricao.Add(RespostaDoCpf(id, cifrado));
        AdicionarRespostasOpcionais(context, id, dados);
        await context.SaveChangesAsync();
        return id;
    }

    private static InscricaoRecord Inscricao(Guid id, byte[] indice, DadosDaInscricaoDoEnem dados) => new()
    {
        Id = id,
        ProcessoSeletivoId = dados.ProcessoId,
        CpfIndice = indice,
        ContaId = $"conta-{id:N}",
        EmissorToken = "https://issuer",
        Situacao = dados.Situacao,
        VersaoFormularioId = dados.VersaoFormularioId ?? Guid.CreateVersion7(),
        NumeroInscricao = dados.NumeroDeInscricao ?? $"UNI-{id.ToString("N")[^8..]}",
        Revisao = 1,
        OfertaCursoId = dados.OfertaCursoId,
        OfertaCursoSegundaOpcaoId = dados.OfertaCursoSegundaOpcaoId,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static RespostaDaInscricaoRecord RespostaDoCpf(Guid inscricaoId, byte[] cifrado) => new()
    {
        Id = Guid.CreateVersion7(),
        InscricaoId = inscricaoId,
        FatoCodigo = ConjuntoBasicoDaInscricao.Cpf,
        Origem = OrigemDaResposta.Informada,
        Tipo = TipoDoValorDeResposta.Texto,
        ValorCifrado = cifrado,
    };

    private static void AdicionarRespostasOpcionais(UniPlusDbContext context, Guid inscricaoId, DadosDaInscricaoDoEnem dados)
    {
        if (dados.Edicao is not null)
        {
            context.RespostasDaInscricao.Add(RespostaDaEdicao(inscricaoId, dados.Edicao));
        }

        if (dados.Nome is not null)
        {
            context.RespostasDaInscricao.Add(RespostaDoNome(inscricaoId, dados.Nome));
        }
    }

    private static RespostaDaInscricaoRecord RespostaDoNome(Guid inscricaoId, string nome) => new()
    {
        Id = Guid.CreateVersion7(),
        InscricaoId = inscricaoId,
        FatoCodigo = ConjuntoBasicoDaInscricao.Nome,
        Origem = OrigemDaResposta.Informada,
        Tipo = TipoDoValorDeResposta.Texto,
        ValorTexto = nome,
    };

    public static async Task TrocarEdicaoAsync(InscricaoDbFixture fixture, Guid inscricaoId, string novaEdicao)
    {
        await using UniPlusDbContext context = fixture.CreateDbContext();
        await context.RespostasDaInscricao
            .Where(r => r.InscricaoId == inscricaoId && r.FatoCodigo == EdicaoDoEnem.Fato)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.ValorCodigo, novaEdicao));
    }

    private static RespostaDaInscricaoRecord RespostaDaEdicao(Guid inscricaoId, string edicao) => new()
    {
        Id = Guid.CreateVersion7(),
        InscricaoId = inscricaoId,
        FatoCodigo = EdicaoDoEnem.Fato,
        Origem = OrigemDaResposta.Informada,
        Tipo = TipoDoValorDeResposta.Opcao,
        ValorCodigo = edicao,
    };
}

internal sealed record DadosDaInscricaoDoEnem(
    Guid ProcessoId, PersonasDeTeste.PessoaFicticia Pessoa, SituacaoInscricao Situacao, string? Edicao)
{
    public string? NumeroDeInscricao { get; init; }
    public Guid? OfertaCursoId { get; init; }
    public Guid? OfertaCursoSegundaOpcaoId { get; init; }
    public Guid? VersaoFormularioId { get; init; }
    public string? Nome { get; init; }
}
