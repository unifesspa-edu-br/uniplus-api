namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using System.Text;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Semeia, direto nas tabelas, o que as notas do ENEM leem de uma inscrição: o CPF cifrado com o
/// índice cego, a situação e a resposta da edição escolhida. Evita montar o fluxo inteiro da
/// inscrição para provar um cruzamento.
/// </summary>
internal static class InscricoesDoEnemDeTeste
{
    public static async Task<ProcessoSeletivo> NovoProcessoAsync(SelecaoDbContext context)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2027 — notas do ENEM", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        context.ProcessosSeletivos.Add(processo);
        await context.SaveChangesAsync();
        return processo;
    }

    /// <summary>Grava a inscrição da persona; <paramref name="edicao"/> nulo deixa a inscrição sem a resposta da edição.</summary>
    public static async Task<Guid> NovaInscricaoAsync(
        InscricaoDbFixture fixture,
        Guid processoId,
        PersonasDeTeste.PessoaFicticia pessoa,
        SituacaoInscricao situacao,
        string? edicao,
        string? numeroDeInscricao = null)
    {
        byte[] cifrado = await fixture.Encryption.EncryptAsync(
            SelecaoEncryptionKeys.IdentificadoresAesGcm, Encoding.UTF8.GetBytes(pessoa.Cpf));
        byte[] indice = await new CalculadorDoIndiceCegoDoCpf(fixture.BlindIndex).ComputarAsync(pessoa.Cpf);

        Guid id = Guid.CreateVersion7();
        await using SelecaoDbContext context = fixture.CreateDbContext();
        context.Inscricoes.Add(new InscricaoRecord
        {
            Id = id,
            ProcessoSeletivoId = processoId,
            CpfCiphertext = cifrado,
            CpfIndice = indice,
            Sub = $"sub-{id:N}",
            EmissorToken = "https://issuer",
            Situacao = situacao,
            VersaoFormularioId = Guid.CreateVersion7(),
            NumeroInscricao = numeroDeInscricao ?? $"UNI-{id.ToString("N")[^8..]}",
            Revisao = 1,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        if (edicao is not null)
        {
            context.RespostasDaInscricao.Add(RespostaDaEdicao(id, edicao));
        }

        await context.SaveChangesAsync();
        return id;
    }

    public static async Task TrocarEdicaoAsync(InscricaoDbFixture fixture, Guid inscricaoId, string novaEdicao)
    {
        await using SelecaoDbContext context = fixture.CreateDbContext();
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
        Tipo = TipoDoValorDeResposta.Codigo,
        ValorCodigo = edicao,
    };
}
