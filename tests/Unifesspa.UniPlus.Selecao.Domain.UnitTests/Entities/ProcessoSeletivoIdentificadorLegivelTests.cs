namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Identificador legível do processo seletivo: livre em rascunho e exigido para gerar versão. Depois da
/// publicação, a configuração só muda pela retificação, que confere o identificador contra a versão
/// vigente (<see cref="Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao.ConferenciaDoIdentificadorDaRetificacao"/>).
/// </summary>
public sealed class ProcessoSeletivoIdentificadorLegivelTests
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly byte[] BytesCanonicos = Encoding.UTF8.GetBytes(new JsonObject { ["status"] = "ok" }.ToJsonString());
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Criar sem identificador legível é recusado")]
    public void Criar_SemIdentificador_Recusa()
    {
        Action criar = () => ProcessoSeletivo.Criar(
            "PS 2026", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, default);

        criar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Em rascunho, o identificador pode ser trocado, mas nunca removido")]
    public void Rascunho_IdentificadorLivre()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        IdentificadorLegivel outro = Identificador("psiq-2026");

        processo.DefinirIdentificadorLegivel(outro).IsSuccess.Should().BeTrue();
        processo.IdentificadorLegivel.Should().Be(outro);

        processo.DefinirIdentificadorLegivel(Identificador("psiq-2026b")).IsSuccess.Should().BeTrue();
        processo.IdentificadorLegivel!.Value.Valor.Should().Be("psiq-2026b");

        Action remover = () => processo.DefinirIdentificadorLegivel(default);
        remover.Should().Throw<ArgumentException>();
        processo.IdentificadorLegivel!.Value.Valor.Should().Be("psiq-2026b");
    }

    [Fact(DisplayName = "Publicar sem identificador legível é recusado com erro nomeado")]
    public void Publicar_SemIdentificador_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        IdentificadoresDeTeste.Retirar(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelAusente);
        processo.Situacao.Should().Be(SituacaoDoProcessoSeletivo.Rascunho);
    }

    [Fact(DisplayName = "O checklist de conformidade aponta o identificador ausente, e só ele")]
    public void Conformidade_SemIdentificador_ApontaOItem()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        IdentificadoresDeTeste.Retirar(processo);

        IReadOnlyList<ItemConformidade> itens = ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo));

        itens.Where(static i => !i.Ok).Select(static i => i.Codigo)
            .Should().Equal("identificador_legivel_nao_declarado");
        itens.Single(static i => i.Codigo == "identificador_legivel_nao_declarado").Dimensao
            .Should().Be(DimensaoConformidade.Identificacao);
    }

    [Fact(DisplayName = "Processo publicado recusa a troca do identificador pela guarda de edição")]
    public void Publicado_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        IdentificadorLegivel original = processo.IdentificadorLegivel!.Value;
        Publicar(processo).IsSuccess.Should().BeTrue();

        processo.DefinirIdentificadorLegivel(Identificador("psiq-2026"))
            .Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        processo.IdentificadorLegivel.Should().Be(original);
    }

    [Fact(DisplayName = "Retificar processo publicado sem identificador é recusado, sem gerar versão")]
    public void Retificar_SemIdentificador_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        VersaoConfiguracao versao = Publicar(processo).Value!;
        IdentificadoresDeTeste.Retirar(processo);

        Result<VersaoConfiguracao> resultado = processo.Retificar(
            new CenarioDeCongelamento { Dados = ProcessoConformeFactory.Dados(), Documento = new DocumentoCongelado(BytesCanonicos, "1.1", "canonical-json/sha256@v1", HashFixo), AtorUsuarioContaId = "user-conta-1", Relogio = new RelogioFixo(Agora.AddMinutes(1)) }, versao, "Correção");

        resultado.IsFailure.Should().BeTrue("nenhuma versão nova é aberta");
        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelAusente);
    }

    private static IdentificadorLegivel Identificador(string valor) => IdentificadorLegivel.Criar(valor).Value;

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) => processo.Publicar(
        new CenarioDeCongelamento { Dados = ProcessoConformeFactory.Dados(), Documento = new DocumentoCongelado(BytesCanonicos, "1.1", "canonical-json/sha256@v1", HashFixo), AtorUsuarioContaId = "user-conta-1", Relogio = new RelogioFixo(Agora) });

    private sealed class RelogioFixo(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}
