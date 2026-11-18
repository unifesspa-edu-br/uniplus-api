namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDaInscricaoGravada;

/// <summary>
/// A inscrição gravada volta inteira do banco pelo repositório: o CPF, as respostas, os documentos dentro de grupos
/// repetíveis, os aceites, a inscrição ativa do CPF no processo e as de todos os processos.
/// </summary>
[Trait("Category", "Integration")]
[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class LeituraDaInscricaoGravadaTests
{
    private readonly CenarioDaInscricaoGravada _cenario;

    public LeituraDaInscricaoGravadaTests(InscricaoDbFixture fixture) => _cenario = new CenarioDaInscricaoGravada(fixture);

    [Fact(DisplayName = "Persistir e ler faz round-trip do CPF, das respostas e dos documentos dentro de um grupo")]
    public async Task Persistir_E_Ler_FazRoundTripCompleto()
    {
        Inscricao original = InscricaoComRespostasEGrupo();
        await _cenario.GravarAsync(original);

        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        InscricaoRepository repositorio = _cenario.Repositorio(contexto);
        Inscricao? lida = await repositorio.ObterPorIdAsync(original.Id);

        InscricaoRecord registro = await contexto.Set<InscricaoRecord>().SingleAsync(r => r.Id == original.Id);
        registro.CreatedAt.Should().NotBe(default, "o repositório registra a data de criação");
        lida!.CreatedAt.Should().Be(registro.CreatedAt);
        lida.UpdatedAt.Should().BeNull();
        ConferirInscricaoLida(lida);
        await ConferirIdentificadoresAsync(repositorio, original.Id);
    }

    [Fact]
    public async Task Atualizar_InscricaoGravada_PreservaCriacaoERetornaAtualizacao()
    {
        Inscricao original = InscricaoComRespostasEGrupo();
        await _cenario.GravarAsync(original);
        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        InscricaoRepository repositorio = _cenario.Repositorio(contexto);
        Inscricao lida = (await repositorio.ObterPorIdAsync(original.Id))!;
        DateTimeOffset criacao = lida.CreatedAt;

        await repositorio.AtualizarAsync(lida);
        await contexto.SaveChangesAsync();
        Inscricao relida = await _cenario.LerAsync(original.Id);
        InscricaoRecord registro = await contexto.Set<InscricaoRecord>().AsNoTracking().SingleAsync(r => r.Id == original.Id);
        relida.CreatedAt.Should().Be(criacao);
        relida.UpdatedAt.Should().Be(registro.UpdatedAt).And.NotBeNull();
    }

    [Fact(DisplayName = "ObterPorProcessoECpf localiza a inscrição pelo CPF do dono, sem decifrar a tabela")]
    public async Task ObterPorProcessoECpf_LocalizaPeloDono()
    {
        Guid processo = Guid.CreateVersion7();
        Inscricao original = NovaInscricao(PersonasDeTeste.KevinCarvalho, processo);
        await _cenario.GravarAsync(original);

        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        InscricaoRepository repositorio = _cenario.Repositorio(contexto);
        Inscricao? lida = await repositorio.ObterAtivaPorProcessoECpfAsync(processo, PersonasDeTeste.KevinCarvalho);

        lida.Should().NotBeNull();
        lida!.Id.Should().Be(original.Id);
        (await repositorio.ObterAtivaPorProcessoECpfAsync(Guid.CreateVersion7(), PersonasDeTeste.KevinCarvalho))
            .Should().BeNull("o mesmo CPF noutro processo não é a mesma inscrição");
    }

    [Fact(DisplayName = "O rascunho recém-iniciado volta do banco com o CPF lido da resposta, e a revelação o devolve")]
    public async Task Rascunho_ReidrataECpfSaiDaResposta()
    {
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        await _cenario.GravarAsync(original);

        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        InscricaoRepository leitura = _cenario.Repositorio(contexto);
        Inscricao lida = (await leitura.ObterPorIdAsync(original.Id))!;

        lida.Titular.Cpf.Valor.Should().Be(PersonasDeTeste.FernandaDrumond.Valor);
        lida.Formulario.Preenchimento.Respostas["CPF"].GetString().Should().Be(PersonasDeTeste.FernandaDrumond.Valor);
        RespostaIdentificadora cpf = (await leitura.LocalizarIdentificadorAsync(original.Id, "CPF", null, null))!;
        (await leitura.LerIdentificadorAsync(cpf.RespostaId)).Should().Be(PersonasDeTeste.FernandaDrumond.Valor);
    }

    [Fact(DisplayName = "A inscrição enviada e gravada recusa salvar rascunho e continua como foi enviada")]
    public async Task Salvar_DepoisDeSubmeter_RecusaEPreservaOEnviado()
    {
        Inscricao original = NovaInscricao(PersonasDeTeste.KevinCarvalho, Guid.CreateVersion7());
        Enviar(original, "000654", new ConteudoDoEnvio { Respostas = Respostas(("NOME_SOCIAL", "Kevin")), Derivados = Respostas(("UF_RESIDENCIA", "PA")) })
            .IsSuccess.Should().BeTrue();
        await _cenario.GravarAsync(original);

        await RecusarSalvarRascunhoDaEnviadaAsync(original.Id);

        Inscricao relida = await _cenario.LerAsync(original.Id);
        relida.Formulario.Derivados["UF_RESIDENCIA"].GetString().Should().Be("PA");
        relida.Formulario.Preenchimento.Respostas["NOME_SOCIAL"].GetString().Should().Be("Kevin");
    }

    [Fact(DisplayName = "ObterTodasPorCpf devolve as inscrições do CPF em todos os processos, nunca as de outro CPF (issue #1860)")]
    public async Task ObterTodasPorCpf_DevolveTodasDoCpfEmQualquerProcesso()
    {
        Inscricao naA = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        Inscricao naB = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        Inscricao deOutroCpf = NovaInscricao(PersonasDeTeste.KevinCarvalho, Guid.CreateVersion7());
        await _cenario.GravarAsync(naA);
        await _cenario.GravarAsync(naB);
        await _cenario.GravarAsync(deOutroCpf);

        // A fixture é compartilhada: outros testes já gravaram inscrições da mesma persona. A prova é que as duas
        // NOVAS aparecem e nenhuma do outro CPF aparece, não que a lista inteira do CPF seja só estas duas.
        IReadOnlySet<Guid> ids = await IdentificadoresDasInscricoesDoCpfAsync(PersonasDeTeste.FernandaDrumond);

        ids.Should().Contain([naA.Id, naB.Id]);
        ids.Should().NotContain(deOutroCpf.Id);
    }

    private static Inscricao InscricaoComRespostasEGrupo()
    {
        Inscricao inscricao = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(OfertaDoProcesso, null), Respostas(("NOME_SOCIAL", "Ariel Drumond"), ("RG_NUMERO", "12.868.927-4")), GrupoFamiliarComUmMembro(), [AceiteDoTermoDeVeracidade()]), Admitidas, PrecondicaoDe(inscricao));

        return inscricao;
    }

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> GrupoFamiliarComUmMembro() =>
        new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] =
            [
                new OcorrenciaDeGrupoInscricao("membro-1", Respostas(("RG_NUMERO", "25.062.156-3"), ("PARENTESCO", "FILHO"))),
            ],
        };

    private static void ConferirInscricaoLida(Inscricao? lida)
    {
        lida.Should().NotBeNull();
        lida!.Titular.Cpf.Valor.Should().Be(PersonasDeTeste.FernandaDrumond.Valor);
        lida.Formulario.Preenchimento.Respostas.Should().ContainKey("RG_NUMERO").WhoseValue.GetString().Should().Be("12.868.927-4");
        lida.Formulario.Preenchimento.Respostas.Should().ContainKey("NOME_SOCIAL");
        lida.Formulario.Preenchimento.Grupos.Should().ContainKey("COMPOSICAO_FAMILIAR")
            .WhoseValue.Should().ContainSingle(o => o.Id == "membro-1")
            .Which.Respostas.Should().ContainKey("RG_NUMERO").WhoseValue.GetString().Should().Be("25.062.156-3");
        lida.Formulario.Preenchimento.Aceites.Should().ContainSingle(a => a.Codigo == "TERMO_VERACIDADE");
    }

    private static async Task ConferirIdentificadoresAsync(InscricaoRepository repositorio, Guid inscricaoId)
    {
        RespostaIdentificadora doMembro = (await repositorio.LocalizarIdentificadorAsync(inscricaoId, "RG_NUMERO", "COMPOSICAO_FAMILIAR", "membro-1"))!;
        (await repositorio.LerIdentificadorAsync(doMembro.RespostaId)).Should().Be("25.062.156-3");
        RespostaIdentificadora daRaiz = (await repositorio.LocalizarIdentificadorAsync(inscricaoId, "RG_NUMERO", null, null))!;
        (await repositorio.LerIdentificadorAsync(daRaiz.RespostaId)).Should().Be("12.868.927-4");
        (await repositorio.LocalizarIdentificadorAsync(inscricaoId, "RG_NUMERO", "COMPOSICAO_FAMILIAR", "membro-inexistente")).Should().BeNull();
        (await repositorio.LocalizarIdentificadorAsync(inscricaoId, "RG_NUMERO", "COMPOSICAO_FAMILIAR", null)).Should().BeNull("o campo em grupo exige a ocorrência");
    }

    private async Task RecusarSalvarRascunhoDaEnviadaAsync(Guid inscricaoId)
    {
        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        Inscricao lida = (await _cenario.Repositorio(contexto).ObterPorIdAsync(inscricaoId))!;

        Result salvo = lida.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(("NOME_SOCIAL", "Kevin C.")), SemGrupos(), []), Admitidas, PrecondicaoDe(lida));

        salvo.Error!.Code.Should().Be(InscricaoErrorCodes.RascunhoSoAntesDoEnvio);
        await contexto.SaveChangesAsync();
    }

    private async Task<IReadOnlySet<Guid>> IdentificadoresDasInscricoesDoCpfAsync(Cpf cpf)
    {
        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        IReadOnlyList<Inscricao> lidas = await _cenario.Repositorio(contexto).ObterTodasPorCpfAsync(cpf);

        return lidas.Select(static i => i.Id).ToHashSet();
    }
}
