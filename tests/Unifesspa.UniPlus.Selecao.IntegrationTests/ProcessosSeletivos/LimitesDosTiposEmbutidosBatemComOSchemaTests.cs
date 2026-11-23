namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.TestesCompartilhados;

using Xunit;

public sealed class LimitesDosTiposEmbutidosBatemComOSchemaTests
{
    public static TheoryData<string, int> Limites
    {
        get
        {
            TheoryData<string, int> dados = [];
            foreach (EntradaDeConfrontoDoLimite entrada in QuadroDeConfrontoDosLimites.Entradas.Where(entrada =>
                entrada.Classe == ClasseDeConfrontoDoLimite.TipoEmbutido && entrada.Nome != nameof(LimitesDoEnvelope.PrecisaoPrazo)))
            {
                dados.Add(entrada.Nome, entrada.Valor);
            }
            return dados;
        }
    }

    [Theory]
    [MemberData(nameof(Limites))]
    public void LimiteDoTipoEmbutido_BateComAsColunas(string nome, int valor)
    {
        using UniPlusDbContext contexto = ModeloEmMemoria.Contexto();
        (Type tipo, string[] propriedades, bool precisao) = ColunasDosTiposEmbutidos.Obter(nome);
        IEntityType[] entidades = contexto.Model.GetEntityTypes().Where(entidade => entidade.ClrType == tipo).ToArray();
        entidades.Should().NotBeEmpty(nome);
        foreach (IEntityType entidade in entidades)
        {
            foreach (string propriedade in propriedades)
            {
                IProperty coluna = entidade.FindProperty(propriedade)!;
                (precisao ? coluna.GetPrecision() : coluna.GetMaxLength()).Should().Be(valor, nome);
            }
        }
    }

    [Fact]
    public void ReferenciasDeRegra_HashTemComprimentoFixo()
    {
        using UniPlusDbContext contexto = ModeloEmMemoria.Contexto();
        IEntityType[] referencias = contexto.Model.GetEntityTypes().Where(entidade => entidade.ClrType == typeof(ReferenciaRegra)).ToArray();
        referencias.Should().NotBeEmpty();
        foreach (IEntityType referencia in referencias)
        {
            IProperty coluna = referencia.FindProperty(nameof(ReferenciaRegra.Hash))!;
            coluna.GetMaxLength().Should().Be(64);
            coluna.IsFixedLength().Should().BeTrue();
        }
    }

    [Fact]
    public void ProvaPresencial_LimitesDoMunicipio_BatemComOSchema()
    {
        using UniPlusDbContext contexto = ModeloEmMemoria.Contexto();
        IEntityType prova = ModeloEmMemoria.EmbutidoDe<ProcessoSeletivo>(contexto, nameof(ProcessoSeletivo.ProvaPresencial));
        IEntityType municipio = prova.FindNavigation(nameof(DeclaracaoDaProvaPresencial.Municipios))!.TargetEntityType;
        municipio.FindProperty(nameof(MunicipioDeProva.CodigoIbge))!.GetMaxLength().Should().Be(7);
        municipio.FindProperty(nameof(MunicipioDeProva.Uf))!.GetMaxLength().Should().Be(2);
        municipio.FindProperty(nameof(MunicipioDeProva.Nome))!.GetMaxLength().Should().Be(ReferenciaCidadeGeo.NomeMaxLength);
    }
}
