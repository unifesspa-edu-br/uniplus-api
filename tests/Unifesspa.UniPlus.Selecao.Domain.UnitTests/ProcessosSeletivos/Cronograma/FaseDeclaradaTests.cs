namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static ModeloDeFase;

public sealed class FaseDeclaradaTests
{
    private static (string? Campo, string Codigo)[] Recusas(FaseDeclarada fase) =>
        [.. fase.Recusas.Select(static r => (r.Field, r.Error.Code))];

    [Fact(DisplayName = "Fase conforme é declarada sem recusas e com os dados como declarados")]
    public void Declarar_FaseConforme_SemRecusas()
    {
        FaseDeclarada fase = new ModeloDeFase("INSCRICAO", 1) { ColetaInscricao = true }.Janela(0, 10).Declarar();

        fase.Recusas.Should().BeEmpty();
        fase.Codigo.Should().Be("INSCRICAO");
        fase.Ordem.Should().Be(1);
        fase.ColetaInscricao.Should().BeTrue();
        fase.TemUnidadeResponsavel.Should().BeTrue();
        fase.Janela.Completa.Should().BeTrue();
    }

    [Fact(DisplayName = "O código é aparado e o período vai a UTC")]
    public void Declarar_NormalizaCodigoEPeriodo()
    {
        TimeSpan emBelem = TimeSpan.FromHours(-3);
        FaseDeclarada fase = new ModeloDeFase("  RESULTADO  ", 2)
        {
            Inicio = new DateTimeOffset(2026, 3, 2, 9, 0, 0, emBelem),
            Fim = new DateTimeOffset(2026, 3, 3, 9, 0, 0, emBelem),
        }.Declarar();

        fase.Codigo.Should().Be("RESULTADO");
        fase.Janela.Inicio.Should().Be(new DateTimeOffset(2026, 3, 2, 12, 0, 0, TimeSpan.Zero));
        fase.Janela.Inicio!.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact(DisplayName = "Fase sem unidade responsável declara a ausência sem recusar a declaração")]
    public void Declarar_SemUnidade_NaoRecusa()
    {
        FaseDeclarada fase = new ModeloDeFase("INSCRICAO", 1) { ComUnidade = false }.Declarar();

        fase.TemUnidadeResponsavel.Should().BeFalse();
        fase.Recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Janela invertida é recusada no fim e os dados continuam declarados")]
    public void Declarar_JanelaInvertida_RecusaNoFimEMantemOsDados()
    {
        FaseDeclarada fase = new ModeloDeFase("INSCRICAO", 1).Janela(5, 1).Declarar();

        Recusas(fase).Should().Equal(("fim", "FaseCronograma.JanelaInvertida"));
        fase.Recusas.Single().Error.Message.Should().Be("A data de término do período é anterior à data de início.");
        fase.Janela.Invertida.Should().BeTrue();
        fase.Ordem.Should().Be(1);
    }

    [Fact(DisplayName = "Banca de análise de recursos com candidatos atendidos é recusada no campo dos candidatos atendidos da banca")]
    public void Declarar_CandidatosAtendidosEmBancaDeRecursos_RecusaNosCandidatosAtendidos()
    {
        BancaRequerida comCandidatosAtendidos = BancaRequerida.Criar(
            TiposDeBancaCopiadosDeTeste.DeRecursos(Guid.CreateVersion7()),
            [CategoriaJulgada.Criar(Guid.CreateVersion7(), "RENDA")]);
        ModeloDeFase modelo = new("RECURSO", 1) { Bancas = [comCandidatosAtendidos] };

        FaseDeclarada fase = modelo.Declarar();

        Recusas(fase).Should().Equal(("bancasRequeridas[0].categoriasDocumentoIds", "Banca.CandidatosAtendidosEmBancaDeRecursos"));
        fase.Recusas[0].Error.Message.Should().NotContain("RECURSO'").And.Contain("Na fase, a banca");
    }

    [Fact(DisplayName = "A declaração traz as mesmas recusas que a criação da fase para a mesma entrada")]
    public void Declarar_MesmasRecusasQueACriacao()
    {
        ModeloDeFase modelo = new ModeloDeFase("INSCRICAO", 1) { EmiteParecerIndividual = true }.Janela(5, 1);

        Result<FaseCronograma> criada = FaseCronograma.Criar(modelo.Origem(), modelo.Decisoes(), modelo.Publicacoes(), modelo.Bancas);

        criada.IsFailure.Should().BeTrue();
        criada.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(Recusas(modelo.Declarar()));
    }

    [Fact(DisplayName = "Declaração nula ou sem identificador é erro de programação")]
    public void Declarar_EntradaInvalida_Lanca()
    {
        ModeloDeFase modelo = new("INSCRICAO", 1);

        Action semOrigem = () => FaseDeclarada.Declarar(Guid.CreateVersion7(), null!, modelo.Decisoes(), modelo.Publicacoes(), []);
        Action semId = () => FaseDeclarada.Declarar(Guid.Empty, modelo.Origem(), modelo.Decisoes(), modelo.Publicacoes(), []);

        semOrigem.Should().Throw<ArgumentNullException>();
        semId.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Os atos da fase distinguem o ciclo que ela abre e o que fecha sozinha")]
    public void Atos_DistinguemOCicloRecursal()
    {
        FaseDeclarada fase = new ModeloDeFase("RESULTADO", 3)
            .Publicando(Preliminar("CLASSIFICACAO"), Definitivo("CLASSIFICACAO"), Preliminar("GABARITO"), Definitivo("HOMOLOGACAO"))
            .Declarar();

        fase.Atos.ProduzResultado.Should().BeTrue();
        fase.Atos.PublicaResultadoPreliminar.Should().BeTrue();
        fase.Atos.TiposComCicloAberto().Should().Equal("GABARITO");
        fase.Atos.TiposFechadosNaPropriaFase().Should().Equal("CLASSIFICACAO");
        fase.Atos.Produtos.Select(static p => p.Posicao).Should().Equal(0, 1, 2, 3);
    }

    [Fact(DisplayName = "As bancas da fase dizem se a de análise de recursos foi declarada e onde cada uma está")]
    public void Bancas_DizemOndeEstaACadaUma()
    {
        FaseDeclarada fase = new ModeloDeFase("RECURSO", 1) { Bancas = [BancasDeTeste.DeRecursosDaFase()] }.Declarar();

        fase.Bancas.TemBancaDeRecursos.Should().BeTrue();
        fase.Bancas.Declaradas.Should().ContainSingle().Which.Should().Match<BancaDeclarada>(
            static banca => banca.Posicao == 0 && banca.Tipo.JulgaRecurso && banca.Regra != null);
    }
}
