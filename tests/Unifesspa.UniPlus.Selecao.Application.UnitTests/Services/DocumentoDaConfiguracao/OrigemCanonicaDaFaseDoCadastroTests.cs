namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>A fase do cronograma guarda os traços que a fase do cadastro tinha ao ser copiada; o cadastro alterado depois não os muda.</summary>
public sealed class OrigemCanonicaDaFaseDoCadastroTests
{
    private static FaseCanonicaView Cadastro(bool organizadaEmChamadas) => new(
        new Guid("4444dddd-0000-4000-8000-000000000005"), "HABILITACAO", "Habilitação", null,
        AgrupaEtapas: false, PermiteComplementacao: false, BaseLegal: null, ColetaInscricao: false, ColetaSolicitacaoIsencao: false,
        OrganizadaEmChamadas: organizadaEmChamadas, OrigemData: "NO_EDITAL");

    private static FaseCronograma FaseCopiadaDe(FaseCanonicaView cadastro) => FaseCronograma.Criar(
        ChaveLocal.Criar("fase").Value!,
        OrigemCanonicaDaFaseDoCadastro.De(cadastro).ComNome(cadastro.Nome),
        new DecisoesDeCronogramaDaFase(1, new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero), UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    [Theory(DisplayName = "A origem copiada do cadastro leva a marca de organizada em chamadas à fase do cronograma")]
    [InlineData(true)]
    [InlineData(false)]
    public void De_LevaAMarcaDoCadastro(bool organizada)
    {
        OrigemCanonicaDaFaseDoCadastro.De(Cadastro(organizada)).OrganizadaEmChamadas.Should().Be(organizada);
        FaseCopiadaDe(Cadastro(organizada)).OrganizadaEmChamadas.Should().Be(organizada);
    }

    [Fact(DisplayName = "Alterar a marca no cadastro depois da cópia não muda a fase já copiada")]
    public void CadastroAlteradoDepois_NaoMudaAFaseCopiada()
    {
        FaseCronograma copiada = FaseCopiadaDe(Cadastro(organizadaEmChamadas: true));

        FaseCronograma recopiadaDoCadastroAlterado = FaseCopiadaDe(Cadastro(organizadaEmChamadas: false));

        copiada.OrganizadaEmChamadas.Should().BeTrue();
        recopiadaDoCadastroAlterado.OrganizadaEmChamadas.Should().BeFalse();
    }
}
