namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using System.Globalization;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Publicacoes.Application.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

/// <summary>
/// Integração do cadastro de tipos de ato contra Postgres real: persistência e
/// auditoria, a exclusion constraint que impede duas versões vivas do mesmo código
/// valerem no mesmo dia, os CHECKs de formato e de janela, e a resolução da versão
/// vigente numa data.
/// </summary>
/// <remarks>
/// O nome da exclusion constraint é o que a camada Application usa para distinguir a violação de
/// qualquer outra exclusion constraint futura: se o PostgreSQL deixasse de informá-lo, a tradução
/// para erro de domínio falharia em silêncio e a corrida viraria 500. O CHECK de janela e o de
/// formato são a defesa contra gravações que não passam pelo agregado (carga inicial, correção manual,
/// ferramenta administrativa), que a validação do domínio já recusa.
/// </remarks>
[Collection(PublicacoesDbCollection.Name)]
public sealed class TipoAtoPublicadoPersistenceTests
{
    private const string Admin = "admin-publicacoes";
    private const string ExclusionViolation = "23P01";
    private const string CheckViolation = "23514";
    private const string NomeDaExclusionConstraint = RestricoesDoBanco.TipoDeAtoPublicadoCodigoVigencia;

    private const string ContypeDaRestricao =
        """
        SELECT contype::text
        FROM pg_constraint c
        JOIN pg_class t ON t.oid = c.conrelid
        JOIN pg_namespace n ON n.oid = t.relnamespace
        WHERE n.nspname = 'publicacoes'
          AND t.relname = 'tipos_de_ato_publicado'
          AND c.conname = @nome
        """;

    private const string InsercaoSemPassarPeloAgregado =
        """
        INSERT INTO publicacoes.tipos_de_ato_publicado
            (id, codigo, nome, congela_configuracao, unico_por_objeto, efeito_irreversivel,
             eh_resultado, exige_ato_corrigido, publicavel_como_avulso,
             vigencia_inicio, vigencia_fim, created_at, is_deleted)
        VALUES
            (gen_random_uuid(), @codigo, 'Gravado sem o agregado', false, false, false,
             false, false, false,
             @inicio, @fim, now(), false)
        """;

    private static readonly DateOnly Inicio = new(2026, 1, 1);
    private static readonly DateOnly Meio = new(2026, 6, 1);
    private static readonly DateOnly Fim = new(2027, 1, 1);

    private readonly PublicacoesDbFixture _fixture;

    public TipoAtoPublicadoPersistenceTests(PublicacoesDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Persiste os campos e a autoria da criação")]
    public async Task Insert_VersaoValida_PersisteCamposEAutoria()
    {
        string codigo = CodigoUnico();
        TipoAtoPublicado tipo = Novo(codigo, Inicio, vigenciaFim: null, congela: true, irreversivel: true);
        await Gravar(tipo);

        TipoAtoPublicado persistido = await LerPersistidoAsync(tipo.Id);

        persistido.Codigo.Should().Be(codigo);
        persistido.CongelaConfiguracao.Should().BeTrue();
        persistido.EfeitoIrreversivel.Should().BeTrue();
        persistido.EhResultado.Should().BeFalse();
        persistido.VigenciaFim.Should().BeNull();
        persistido.CreatedBy.Should().Be(Admin);
        persistido.IsDeleted.Should().BeFalse();
    }

    /// <summary>
    /// A constraint vive em SQL escrito à mão, fora do modelo gerado: reunir as migrations a descartaria
    /// em silêncio, e todos os testes de sobreposição continuariam passando pela validação da
    /// aplicação. Esta asserção olha o catálogo do Postgres.
    /// </summary>
    [Fact(DisplayName = "A exclusion constraint existe no banco como constraint de exclusão")]
    public async Task ExclusionConstraint_ConsultadaNoCatalogo_EhDoTipoExclusao()
    {
        object? contype = await ConsultarContypeAsync(NomeDaExclusionConstraint);

        contype.Should().Be("x", "'x' é o contype de EXCLUDE no pg_constraint");
    }

    [Fact(DisplayName = "Duas versões vivas do mesmo código com janelas que se cruzam são recusadas")]
    public async Task Insert_ComVigenciaSobreposta_FalhaComViolacaoDeExclusao()
    {
        string codigo = CodigoUnico();
        await Gravar(Novo(codigo, Inicio, Fim));

        Func<Task> segundaInsercao = () => Gravar(Novo(codigo, Meio, Fim.AddYears(1)));

        DbUpdateException erro = (await segundaInsercao.Should().ThrowAsync<DbUpdateException>()).Which;
        PostgresException pg = erro.InnerException.Should().BeOfType<PostgresException>().Which;
        pg.SqlState.Should().Be(ExclusionViolation);
        pg.ConstraintName.Should().Be(NomeDaExclusionConstraint);
    }

    [Fact(DisplayName = "Vigência aberta cruza qualquer janela futura do mesmo código")]
    public async Task Insert_ComVigenciaAbertaEOutraFutura_Falha()
    {
        string codigo = CodigoUnico();
        await Gravar(Novo(codigo, Inicio, vigenciaFim: null));

        Func<Task> segundaInsercao = () => Gravar(Novo(codigo, Fim, vigenciaFim: null));

        DbUpdateException erro = (await segundaInsercao.Should().ThrowAsync<DbUpdateException>()).Which;
        erro.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(ExclusionViolation);
    }

    [Fact(DisplayName = "Sucessão adjacente é aceita e, no dia da fronteira, vigora exatamente uma versão")]
    public async Task Insert_ComJanelasAdjacentes_AceitaEResolveUmaSo()
    {
        string codigo = CodigoUnico();
        TipoAtoPublicado antiga = Novo(codigo, Inicio, Meio, congela: false);
        TipoAtoPublicado nova = Novo(codigo, Meio, vigenciaFim: null, congela: true);
        await Gravar(antiga);
        await Gravar(nova);

        // Véspera: a antiga. Dia da fronteira: já a nova — o fim é exclusivo.
        (await VigenteEmAsync(codigo, Meio.AddDays(-1)))!.Id.Should().Be(antiga.Id);
        (await VigenteEmAsync(codigo, Meio))!.Id.Should().Be(nova.Id);
        (await VigenteEmAsync(codigo, Meio.AddYears(10)))!.Id.Should().Be(nova.Id);
    }

    [Fact(DisplayName = "A versão vigente numa data histórica é a que valia então")]
    public async Task ObterVigente_EmDataHistorica_DevolveVersaoDaEpoca()
    {
        string codigo = CodigoUnico();
        TipoAtoPublicado antiga = Novo(codigo, Inicio, Meio, congela: false);
        await Gravar(antiga);
        await Gravar(Novo(codigo, Meio, vigenciaFim: null, congela: true));

        TipoAtoPublicado? vigenteEmMarco = await VigenteEmAsync(codigo, new DateOnly(2026, 3, 15));

        vigenteEmMarco!.Id.Should().Be(antiga.Id);
        vigenteEmMarco.CongelaConfiguracao.Should().BeFalse(
            "o que o catálogo dizia em março de 2026 não muda porque o tipo foi reeditado depois");
    }

    [Fact(DisplayName = "Antes do início da primeira versão não há tipo vigente")]
    public async Task ObterVigente_AntesDoInicio_DevolveNulo()
    {
        string codigo = CodigoUnico();
        await Gravar(Novo(codigo, Inicio, vigenciaFim: null));

        (await VigenteEmAsync(codigo, Inicio.AddDays(-1))).Should().BeNull();
    }

    /// <summary>
    /// Sem <c>IgnoreQueryFilters</c> a linha some — e sumir é indistinguível de ter sido apagada. A
    /// afirmação de que o delete é lógico só se sustenta olhando a linha por trás do filtro.
    /// </summary>
    [Fact(DisplayName = "Remover marca a linha como excluída em vez de apagá-la")]
    public async Task Remover_VersaoExistente_MarcaComoExcluida()
    {
        TipoAtoPublicado original = Novo(CodigoUnico(), Inicio, vigenciaFim: null);
        await Gravar(original);
        await Excluir(original.Id);

        TipoAtoPublicado excluido = await LerExcluidoAsync(original.Id);

        excluido.IsDeleted.Should().BeTrue();
        excluido.DeletedAt.Should().NotBeNull();
        excluido.DeletedBy.Should().Be(Admin);
        (await ExisteNaLeituraNormalAsync(original.Id)).Should().BeFalse("a leitura normal oculta o registro excluído");
    }

    /// <summary>
    /// A versão excluída continua no banco, com a mesma janela — a exclusion constraint a ignora
    /// porque o predicado dela é <c>is_deleted = false</c>.
    /// </summary>
    [Fact(DisplayName = "Soft-delete libera a janela para uma nova versão do mesmo código")]
    public async Task Remover_VersaoExcluida_LiberaAJanelaParaNovaVersao()
    {
        string codigo = CodigoUnico();
        TipoAtoPublicado original = Novo(codigo, Inicio, vigenciaFim: null);
        await Gravar(original);
        await Excluir(original.Id);
        TipoAtoPublicado recriado = Novo(codigo, Inicio, vigenciaFim: null);
        await Gravar(recriado);

        (await VigenteEmAsync(codigo, Inicio))!.Id.Should().Be(recriado.Id);
        (await ContarIncluindoExcluidosAsync(codigo)).Should().Be(2);
    }

    [Fact(DisplayName = "A restrição de vigência do banco recusa janela vazia gravada sem passar pelo agregado")]
    public async Task Gravar_JanelaVaziaSemPassarPeloAgregado_ViolaARestricaoDeVigencia()
    {
        Func<Task> insercao = () => InserirSemPassarPeloAgregado(CodigoUnico(), Inicio, Inicio);

        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    [Fact(DisplayName = "A restrição de formato do banco recusa código fora do formato gravado sem passar pelo agregado")]
    public async Task Gravar_CodigoForaDoFormatoSemPassarPeloAgregado_ViolaARestricaoDeFormato()
    {
        Func<Task> insercao = () => InserirSemPassarPeloAgregado("edital abertura", Inicio, null);

        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    [Fact(DisplayName = "ExisteSobreposicaoDeVigencia detecta a janela que cruza")]
    public async Task ExisteSobreposicao_JanelaQueCruza_DevolveVerdadeiro()
    {
        string codigo = CodigoUnico();
        await Gravar(Novo(codigo, Inicio, Fim));

        (await ExisteSobreposicaoAsync(codigo, Meio, null, null)).Should().BeTrue();
    }

    [Fact(DisplayName = "ExisteSobreposicaoDeVigencia não acusa a janela que começa exatamente onde a outra termina")]
    public async Task ExisteSobreposicao_JanelaAdjacentePelaDireita_DevolveFalso()
    {
        string codigo = CodigoUnico();
        await Gravar(Novo(codigo, Inicio, Fim));

        (await ExisteSobreposicaoAsync(codigo, Fim, null, null)).Should().BeFalse();
    }

    [Fact(DisplayName = "ExisteSobreposicaoDeVigencia não acusa a janela que termina exatamente onde a outra começa")]
    public async Task ExisteSobreposicao_JanelaAdjacentePelaEsquerda_DevolveFalso()
    {
        string codigo = CodigoUnico();
        await Gravar(Novo(codigo, Inicio, Fim));

        (await ExisteSobreposicaoAsync(codigo, Inicio.AddYears(-1), Inicio, null)).Should().BeFalse();
    }

    [Fact(DisplayName = "ExisteSobreposicaoDeVigencia ignora a própria versão")]
    public async Task ExisteSobreposicao_ExcluindoAPropriaVersao_DevolveFalso()
    {
        string codigo = CodigoUnico();
        TipoAtoPublicado existente = Novo(codigo, Inicio, Fim);
        await Gravar(existente);

        (await ExisteSobreposicaoAsync(codigo, Inicio, Fim, existente.Id)).Should().BeFalse();
    }

    [Fact(DisplayName = "ExisteSobreposicaoDeVigencia não acusa outro código na mesma janela")]
    public async Task ExisteSobreposicao_OutroCodigoNaMesmaJanela_DevolveFalso()
    {
        await Gravar(Novo(CodigoUnico(), Inicio, Fim));

        (await ExisteSobreposicaoAsync(CodigoUnico(), Inicio, Fim, null)).Should().BeFalse();
    }

    [Fact(DisplayName = "A listagem por vigência inclui o dia do início e exclui o dia do fim")]
    public async Task ListarPaginado_ComDiaDeVigencia_RespeitaInicioInclusivoEFimExclusivo()
    {
        string codigo = CodigoUnico();
        TipoAtoPublicado encerrada = Novo(codigo, Inicio, Meio);
        TipoAtoPublicado sucessora = Novo(codigo, Meio, vigenciaFim: null);
        await Gravar(encerrada);
        await Gravar(sucessora);

        (await IdsListadosAsync(Meio.AddDays(-1))).Should().Contain(encerrada.Id).And.NotContain(sucessora.Id);
        (await IdsListadosAsync(Meio)).Should().Contain(sucessora.Id).And.NotContain(encerrada.Id);
    }

    [Fact(DisplayName = "A listagem sem dia de vigência devolve também a versão encerrada")]
    public async Task ListarPaginado_SemDiaDeVigencia_DevolveAVersaoEncerrada()
    {
        TipoAtoPublicado encerrada = Novo(CodigoUnico(), Inicio, Meio);
        await Gravar(encerrada);

        (await IdsListadosAsync(vigentesEm: null)).Should().Contain(encerrada.Id);
    }

    private async Task Gravar(TipoAtoPublicado tipo)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(Admin);
        contexto.TiposAtoPublicado.Add(tipo);
        await contexto.SaveChangesAsync();
    }

    private async Task Excluir(Guid id)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(Admin);
        TipoAtoPublicado alvo = await contexto.TiposAtoPublicado.SingleAsync(tipo => tipo.Id == id);
        contexto.TiposAtoPublicado.Remove(alvo);
        await contexto.SaveChangesAsync();
    }

    private async Task<TipoAtoPublicado> LerPersistidoAsync(Guid id)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null);
        return await contexto.TiposAtoPublicado.SingleAsync(tipo => tipo.Id == id);
    }

    private async Task<TipoAtoPublicado> LerExcluidoAsync(Guid id)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null);
        return await contexto.TiposAtoPublicado.IgnoreQueryFilters().SingleAsync(tipo => tipo.Id == id);
    }

    private async Task<bool> ExisteNaLeituraNormalAsync(Guid id)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null);
        return await contexto.TiposAtoPublicado.AnyAsync(tipo => tipo.Id == id);
    }

    private async Task<int> ContarIncluindoExcluidosAsync(string codigo)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null);
        return await contexto.TiposAtoPublicado.IgnoreQueryFilters().CountAsync(tipo => tipo.Codigo == codigo);
    }

    private async Task<TipoAtoPublicado?> VigenteEmAsync(string codigo, DateOnly dia)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null);
        return await new TipoAtoPublicadoRepository(contexto).ObterVigenteAsync(codigo, dia, default);
    }

    private async Task<IReadOnlyList<Guid>> IdsListadosAsync(DateOnly? vigentesEm)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null);

        (IReadOnlyList<TipoAtoPublicado> itens, _, _) = await new TipoAtoPublicadoRepository(contexto)
            .ListarPaginadoAsync(null, 100, PaginationDirection.Next, vigentesEm, default);

        return [.. itens.Select(tipo => tipo.Id)];
    }

    private async Task<bool> ExisteSobreposicaoAsync(
        string codigo, DateOnly vigenciaInicio, DateOnly? vigenciaFim, Guid? excluirId)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null);

        return await new TipoAtoPublicadoRepository(contexto)
            .ExisteSobreposicaoDeVigenciaAsync(codigo, vigenciaInicio, vigenciaFim, excluirId, default);
    }

    private async Task<object?> ConsultarContypeAsync(string nomeDaRestricao)
    {
        await using UniPlusDbContext contexto = await ContextoComConexaoAbertaAsync();
        await using var comando = new NpgsqlCommand(ContypeDaRestricao, (NpgsqlConnection)contexto.Database.GetDbConnection());
        comando.Parameters.AddWithValue("nome", nomeDaRestricao);

        return await comando.ExecuteScalarAsync();
    }

    private async Task InserirSemPassarPeloAgregado(string codigo, DateOnly inicio, DateOnly? fim)
    {
        await using UniPlusDbContext contexto = await ContextoComConexaoAbertaAsync();
        await using var comando = new NpgsqlCommand(
            InsercaoSemPassarPeloAgregado, (NpgsqlConnection)contexto.Database.GetDbConnection());
        comando.Parameters.AddWithValue("codigo", codigo);
        comando.Parameters.AddWithValue("inicio", inicio);
        comando.Parameters.AddWithValue("fim", fim ?? (object)DBNull.Value);

        await comando.ExecuteNonQueryAsync();
    }

    private async Task<UniPlusDbContext> ContextoComConexaoAbertaAsync()
    {
        UniPlusDbContext contexto = _fixture.CreateDbContext(userId: null);
        await contexto.Database.OpenConnectionAsync();
        return contexto;
    }

    private static TipoAtoPublicado Novo(
        string codigo,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim,
        bool congela = true,
        bool irreversivel = false,
        bool ehResultado = false) =>
        TipoAtoPublicado.Criar(
            codigo,
            nome: "Tipo de ato de teste",
            congelaConfiguracao: congela,
            unicoPorObjeto: false,
            efeitoIrreversivel: irreversivel,
            ehResultado: ehResultado,
            vigenciaInicio: vigenciaInicio,
            vigenciaFim: vigenciaFim,
            baseLegal: null).Value!;

    /// <summary>
    /// Código único no formato aceito pelo CHECK: os 12 primeiros dígitos hex do
    /// Guid mapeados para as letras A–P, o que evita colisão entre testes que
    /// compartilham o mesmo container.
    /// </summary>
    private static string CodigoUnico()
    {
        string hex = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
        return "TESTE_" + string.Concat(hex.Select(digito => (char)('A' + Convert.ToInt32(digito.ToString(), 16))));
    }
}
