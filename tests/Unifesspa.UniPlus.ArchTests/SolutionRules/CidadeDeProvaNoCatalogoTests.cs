namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Monolito.Persistencia;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class CidadeDeProvaNoCatalogoTests
{
    [Fact]
    public void Semente_CidadeDeProva_DeclaraOsEixosEProtecao()
    {
        FatoCandidatoSeedItem cidade = FatoCandidatoSeed.Itens.Single(item => item.Codigo == "CIDADE_DE_PROVA");
        cidade.Dominio.Should().Be(TipoDoDado.Opcao);
        cidade.Origem.Should().Be(OrigemFato.Declarado);
        cidade.Cardinalidade.Should().Be(QuantidadeDeValores.ValorUnico);
        cidade.FonteValores.Should().Be(FonteValoresFato.MunicipiosProvaPresencial);
        cidade.Binding.Should().Be("CAMPO_FORMULARIO:CIDADE_DE_PROVA");
        cidade.PontoResolucao.Should().Be("INSCRICAO");
        cidade.ClassificacaoProtecao.Should().Be(ClassificacaoDeProtecao.Pessoal);
        cidade.HipoteseLegal.Should().Be(HipoteseLegalTratamento.ExecucaoPoliticasPublicas);
    }

    [Fact]
    public void Modelo_FonteDeMunicipiosDeProva_CabeNaColunaEPassaNoCheck()
    {
        using MonolitoDbContext contexto = new MonolitoDbContextDesignTimeFactory().CreateDbContext([]);
        IEntityType fato = contexto.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(FatoCandidato))!;
        fato.FindProperty(nameof(FatoCandidato.FonteValores))!.GetMaxLength().Should().BeGreaterThanOrEqualTo(27);
        fato.GetCheckConstraints().Single(check => check.Name == "ck_dados_do_candidato_fonte_das_opcoes_coerente")
            .Sql.Should().Contain("'MUNICIPIOS_PROVA_PRESENCIAL'");
    }

}
