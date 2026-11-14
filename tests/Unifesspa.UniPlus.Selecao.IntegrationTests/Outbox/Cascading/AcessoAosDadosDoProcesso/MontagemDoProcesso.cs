namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

using System.Net;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// Monta o processo no banco do host de teste: o rascunho semeado e o processo publicado de verdade, com uma exigência de
/// documento de cada classificação de proteção e o fato de membro de grupo no formulário de inscrição, pelas mesmas
/// requisições administrativas que a produção atende.
/// </summary>
internal sealed class MontagemDoProcesso(CascadingFixture fixture, RequisicoesAutenticadas http)
{
    private static readonly (ClassificacaoDeProtecao Classificacao, string Prefixo, string[] FatosComprovados)[] TiposExigidos =
    [
        (ClassificacaoDeProtecao.Pessoal, TiposDeDocumentoDoCenario.Pessoal, []),
        (ClassificacaoDeProtecao.Sensivel, TiposDeDocumentoDoCenario.Sensivel, [FatosDoCenario.Sensivel]),
        (ClassificacaoDeProtecao.Publico, TiposDeDocumentoDoCenario.Publico, []),
        (ClassificacaoDeProtecao.Interno, TiposDeDocumentoDoCenario.Interno, []),
        (ClassificacaoDeProtecao.Identificador, TiposDeDocumentoDoCenario.Identificador, []),
    ];

    public async Task<Guid> SemearRascunhoAsync(string nome) =>
        (await SemearProcessoAsync(nome)).ProcessoId;

    public async Task<Guid> UnidadeDoProcessoAsync(Guid processoId)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>().ProcessosSeletivos.AsNoTracking()
            .Where(p => p.Id == processoId).Select(p => p.UnidadeAdministradoraOrigemId).SingleAsync();
    }

    public async Task<ProcessoPublicado> PublicarAsync(string nome)
    {
        await TiposDeAtoSeeder.SemearAsync(fixture.Factory.Services);
        (Guid processoId, Guid documentoEdital, Guid faseId) = await SemearProcessoAsync(nome);
        string fatoDoGrupo = await SemearFatoIdentificadorDeMembroAsync();
        object[] exigencias = await SemearExigenciasAsync(faseId);
        string rota = $"/api/selecao/processos-seletivos/{processoId}";

        await ExigirSemConteudoAsync(HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{processoId}/formularios/INSCRICAO/itens", CorposDasRequisicoesDoProcesso.ItensDoFormulario(fatoDoGrupo));
        await ExigirSemConteudoAsync(HttpMethod.Put, $"{rota}/documentos-exigidos", exigencias);
        await ExigirSemConteudoAsync(HttpMethod.Post, $"{rota}/publicacao", CorposDasRequisicoesDoProcesso.CorpoDoAto(documentoEdital));

        return await LerPublicadoAsync(processoId, faseId, fatoDoGrupo);
    }

    private async Task<(Guid ProcessoId, Guid DocumentoEdital, Guid FaseId)> SemearProcessoAsync(string nome)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
        return (processo.Id, documento.Id, processo.CronogramaFases.Single().Id);
    }

    private async Task<object[]> SemearExigenciasAsync(Guid faseId)
    {
        List<object> folhas = [];
        foreach ((ClassificacaoDeProtecao classificacao, string prefixo, string[] fatosComprovados) in TiposExigidos)
        {
            Guid tipoId = await SemearTipoAsync(classificacao, prefixo);
            folhas.Add(CorposDasRequisicoesDoProcesso.Folha(faseId, tipoId, fatosComprovados));
        }

        return [.. folhas];
    }

    private async Task ExigirSemConteudoAsync(HttpMethod metodo, string rota, object corpo)
    {
        using HttpResponseMessage resposta = await http.SendAdminAsync(metodo, rota, corpo);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
    }

    private async Task<ProcessoPublicado> LerPublicadoAsync(Guid processoId, Guid faseId, string fatoDoGrupo)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Guid versaoId = await contexto.VersoesConfiguracao.AsNoTracking().Where(v => v.ProcessoSeletivoId == processoId)
            .OrderByDescending(v => v.NumeroVersao).Select(v => v.Id).FirstAsync();
        List<DocumentoExigido> exigidos = await contexto.Set<DocumentoExigido>().AsNoTracking()
            .Where(d => d.ProcessoSeletivoId == processoId).ToListAsync();

        return new ProcessoPublicado(processoId, await UnidadeDoProcessoAsync(processoId), versaoId, faseId, fatoDoGrupo, exigidos);
    }

    private async Task<string> SemearFatoIdentificadorDeMembroAsync()
    {
        string codigo = $"RG_RESPONSAVEL_{Guid.NewGuid():N}"[..30].ToUpperInvariant();
        Result<FatoCandidato> criado = FatoCandidato.CriarDoAdministrador(
            codigo, "RG do responsável", "Descrição do fato.", DominioFato.Texto, CardinalidadeFato.Escalar, fonteValores: null, formato: FormatoTexto.Livre,
            "INSCRICAO", EscopoFato.MembroGrupo, ClassificacaoDeProtecao.Identificador, "Identificação do responsável legal",
            HipoteseLegalTratamento.CumprimentoObrigacaoLegal);
        criado.IsSuccess.Should().BeTrue(string.Join("; ", criado.Errors.Select(e => e.Error.Message)));
        await GravarNoCatalogoAprovadoAsync(criado.Value!);
        return codigo;
    }

    private async Task<Guid> SemearTipoAsync(ClassificacaoDeProtecao classificacao, string prefixo)
    {
        TipoDocumento tipo = TipoDocumento.Criar(
            $"{prefixo}_{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}", prefixo, null, "SAUDE", null, null, null,
            classificacao, "Comprovação documental do candidato.", HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        await GravarNoCatalogoAprovadoAsync(tipo);
        return tipo.Id;
    }

    private async Task GravarNoCatalogoAprovadoAsync<TEntidade>(TEntidade entidade)
        where TEntidade : class
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        db.Add(entidade);
        await db.SaveChangesAsync();
        await db.AprovarCatalogoAsync();
    }
}
