namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Modalidades;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.Modalidades.ApoioDaPersistenciaDeModalidades;

[Collection(ConfiguracaoDbCollection.Name)]
public sealed class ModalidadeLeituraTests(ConfiguracaoDbFixture fixture)
{
    private readonly ConfiguracaoDbFixture _fixture = fixture;

    [Fact]
    public async Task CodigoExiste_ConsultaVivaEExclusao_RespeitaFormatoEIdentidade()
    {
        Modalidade modalidade = Ampla(CodigoUnico());
        await GravarAsync(_fixture, modalidade);
        await using UniPlusDbContext context = _fixture.CreateDbContext(userId: null);
        ModalidadeRepository repositorio = new(context);

        (await repositorio.CodigoExisteEntreVivosAsync($" {modalidade.Codigo.Valor} ", null, default)).Should().BeFalse();
        (await repositorio.CodigoExisteEntreVivosAsync(modalidade.Codigo.Valor, modalidade.Id, default)).Should().BeFalse();
        (await repositorio.CodigoExisteEntreVivosAsync("código inválido!", null, default)).Should().BeFalse();
    }

    [Fact]
    public async Task CodigosVivos_ComEspacos_Recusa()
    {
        Modalidade primeira = Ampla(CodigoUnico());
        Modalidade segunda = Ampla(CodigoUnico());
        await GravarAsync(_fixture, primeira, segunda);
        await using UniPlusDbContext context = _fixture.CreateDbContext(userId: null);
        ModalidadeRepository repositorio = new(context);

        (await repositorio.CodigosVivosExistemAsync([primeira.Codigo.Valor, $" {primeira.Codigo.Valor} ", segunda.Codigo.Valor, " "], default)).Should().BeFalse();
        (await repositorio.CodigosVivosExistemAsync([primeira.Codigo.Valor, CodigoUnico()], default)).Should().BeFalse();
    }

    [Fact]
    public async Task CodigosVivos_VazioInvalidoOuRemovido_RespeitaCadaCaso()
    {
        Modalidade modalidade = Ampla(CodigoUnico());
        await GravarAsync(_fixture, modalidade);
        await RemoverAsync(_fixture, modalidade.Id);
        await using UniPlusDbContext context = _fixture.CreateDbContext(userId: null);
        ModalidadeRepository repositorio = new(context);

        (await repositorio.CodigosVivosExistemAsync([], default)).Should().BeTrue();
        (await repositorio.CodigosVivosExistemAsync([" ", ""], default)).Should().BeFalse();
        (await repositorio.CodigosVivosExistemAsync(["código inválido!"], default)).Should().BeFalse();
        (await repositorio.CodigosVivosExistemAsync([modalidade.Codigo.Valor], default)).Should().BeFalse();
        (await repositorio.CodigoExisteEntreVivosAsync(modalidade.Codigo.Valor, null, default)).Should().BeFalse();
    }

    [Fact(DisplayName = "A leitura ordena as modalidades vivas e exclui as removidas")]
    public async Task ListarVivos_OrdenaPorCodigoEExcluiSoftDeleted()
    {
        string prefixo = $"MOD_{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        Modalidade excluida = Ampla($"{prefixo}_D");
        await GravarAsync(_fixture, Ampla($"{prefixo}_B"), Ampla($"{prefixo}_A"), excluida);
        await RemoverAsync(_fixture, excluida.Id);
        await using UniPlusDbContext context = _fixture.CreateDbContext(userId: null);
        IReadOnlyList<ModalidadeView> todas = await new ModalidadeReader(context).ListarVivosAsync();
        string[] codigos = [.. todas.Select(v => v.Codigo).Where(c => c.StartsWith(prefixo, StringComparison.Ordinal))];
        codigos.Should().Equal([$"{prefixo}_A", $"{prefixo}_B"]);
    }

    [Fact(DisplayName = "A leitura recusa código com espaços")]
    public async Task ObterVivaPorCodigo_ComEspacos_NaoEncontra()
    {
        Modalidade modalidade = Ampla(CodigoUnico());
        await GravarAsync(_fixture, modalidade);
        await using UniPlusDbContext context = _fixture.CreateDbContext(userId: null);
        ModalidadeReader reader = new(context);
        ModalidadeView? viva = await reader.ObterVivaPorCodigoAsync(modalidade.Codigo.Valor);
        ModalidadeView? comEspaco = await reader.ObterVivaPorCodigoAsync($" {modalidade.Codigo.Valor}  ");
        viva.Should().NotBeNull();
        viva!.Codigo.Should().Be(modalidade.Codigo.Valor);
        comEspaco.Should().BeNull();
    }

    [Fact(DisplayName = "A leitura não devolve modalidade removida")]
    public async Task ObterVivaPorCodigo_Removida_NaoEncontra()
    {
        Modalidade modalidade = Ampla(CodigoUnico());
        await GravarAsync(_fixture, modalidade);
        await RemoverAsync(_fixture, modalidade.Id);
        await using UniPlusDbContext context = _fixture.CreateDbContext(userId: null);
        (await new ModalidadeReader(context).ObterVivaPorCodigoAsync(modalidade.Codigo.Valor)).Should().BeNull();
    }

    [Fact(DisplayName = "A leitura recusa código fora do formato")]
    public async Task ObterVivaPorCodigo_FormatoInvalido_NaoEncontra()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext(userId: null);
        (await new ModalidadeReader(context).ObterVivaPorCodigoAsync("lb ppi!")).Should().BeNull();
    }
}
