namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O tipo do registro que a trilha da contenção do perfil expõe.</summary>
public sealed class TipoDoRegistroDoBloqueioMappingTests
{
    private const string Consultar = "selecao:processos:consultar";

    public static TheoryData<TipoDoEventoDaContencao> Tipos => [.. Enum.GetValues<TipoDoEventoDaContencao>()];

    [Theory(DisplayName = "Cada tipo de evento da contenção vira o registro do bloqueio com o mesmo código")]
    [MemberData(nameof(Tipos))]
    public void Evento_CadaTipo_ViraORegistroComOMesmoCodigo(TipoDoEventoDaContencao tipo)
    {
        EventoDaContencao evento = TrilhaCompleta().Single(e => e.Tipo == tipo);

        RegistroDaContencaoDto registro = ContencaoMapping.ToDto(new TrilhaDaContencao([evento], [])).Single();

        registro.Tipo.Codigo.Should().Be(tipo.Codigo());
    }

    [Fact(DisplayName = "A decisão de uma etapa da retomada é um registro do bloqueio de tipo próprio, de uma pessoa")]
    public void Decisao_ViraORegistroDeDecisaoDaRetomada()
    {
        Perfil perfil = PerfilComTrilhaCompleta();
        DecisaoDaRetomada decisao = perfil.Suspensoes.Single().Decisoes.Single();

        RegistroDaContencaoDto registro = ContencaoMapping.ToDto(new TrilhaDaContencao([], [decisao])).Single();

        (registro.Tipo.Codigo, registro.Ator.Codigo, registro.Etapa?.Codigo, registro.Origem).Should().Be(("DECISAO_DA_RETOMADA", "HUMANO", "GESTAO_DE_ACESSOS", null));
    }

    private static IReadOnlyList<EventoDaContencao> TrilhaCompleta() => PerfilComTrilhaCompleta().Eventos;

    private static Perfil PerfilComTrilhaCompleta()
    {
        DoublesDePerfis.CatalogoDeTeste catalogo = new();
        Perfil perfil = PerfisDeTeste.Vigente("Analista", "Descrição válida.", [Consultar], catalogo);
        perfil.Suspender(PerfisDeTeste.Gestor, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.MotivoDaContencao, PerfisDeTeste.Instante, catalogo);
        perfil.DecidirRetomada(new ContextoDaRetomada(
            PerfisDeTeste.ProtecaoDeDados, true, EtapaDaVersao.GestaoDeAcessos, MotivoInformado.Normalizar(PerfisDeTeste.MotivoDaContencao), catalogo, PerfisDeTeste.Instante));
        PerfisDeTeste.Desativar(perfil, catalogo);
        PerfisDeTeste.Reativar(perfil, catalogo);

        return perfil;
    }
}
