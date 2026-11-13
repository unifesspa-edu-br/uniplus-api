namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.ManifestacoesDeTeste;

/// <summary>
/// As manifestações técnicas de um item num ciclo (UNI-REQ-0373): a de cada pessoa é vigente até ela se manifestar de
/// novo, as de pessoas diferentes coexistem, e o circuito do item guarda se há manifestação desfavorável vigente.
/// </summary>
public sealed class ManifestacoesDoCicloTests
{
    private static readonly DateTimeOffset Instante = TipoDocumentoNaAprovacaoBuilder.Segunda.AddHours(3);

    [Fact(DisplayName = "A primeira manifestação é registrada no ciclo e marca o circuito com o instante e o parecer desfavorável")]
    public void Registrar_Inicial()
    {
        TipoDocumento tipo = new TipoDocumentoNaAprovacaoBuilder().Solicitado().Build();

        Result<ManifestacaoDaProtecaoDoCatalogo> registrada = Do(tipo).Registrar(tipo.Aprovacao, CicloDaAprovacao.Primeiro, Inadequada(), Kevin, Instante);

        registrada.Value!.Should().BeEquivalentTo(new
        {
            Item = ItemDe(tipo),
            Ciclo = CicloDaAprovacao.Primeiro,
            Parecer = ParecerDaManifestacao.Inadequada,
            SubstituiId = (Guid?)null,
            Instante,
        });
        (tipo.Aprovacao.ManifestadaEm, tipo.Aprovacao.ManifestacaoDesfavoravelVigente).Should().Be((Instante, true));
        tipo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Solicitada, "a manifestação não decide");
    }

    [Fact(DisplayName = "A nova manifestação da mesma pessoa substitui a vigente sem alterá-la, e o indicador segue a vigente")]
    public void Registrar_MesmaPessoa_Substitui()
    {
        TipoDocumento tipo = new TipoDocumentoNaAprovacaoBuilder().Solicitado().Build();
        ManifestacaoDaProtecaoDoCatalogo primeira = Do(tipo).Registrar(tipo.Aprovacao, CicloDaAprovacao.Primeiro, Inadequada(), Kevin, Instante).Value!;

        ManifestacaoDaProtecaoDoCatalogo segunda = Do(tipo, primeira)
            .Registrar(tipo.Aprovacao, CicloDaAprovacao.Primeiro, Adequada(), Kevin, Instante.AddMinutes(5)).Value!;

        segunda.SubstituiId.Should().Be(primeira.Id);
        primeira.Parecer.Should().Be(ParecerDaManifestacao.Inadequada, "o evento anterior não muda");
        Do(tipo, primeira, segunda).Vigentes.Should().ContainSingle().Which.Should().BeSameAs(segunda);
        (tipo.Aprovacao.ManifestadaEm, tipo.Aprovacao.ManifestacaoDesfavoravelVigente).Should().Be((Instante.AddMinutes(5), false));
    }

    [Fact(DisplayName = "Manifestação idêntica à vigente entra como nova substituição")]
    public void Registrar_Identica_EntraComoSubstituicao()
    {
        TipoDocumento tipo = new TipoDocumentoNaAprovacaoBuilder().Solicitado().Build();
        ManifestacaoDaProtecaoDoCatalogo primeira = Do(tipo).Registrar(tipo.Aprovacao, CicloDaAprovacao.Primeiro, Adequada(), Kevin, Instante).Value!;

        ManifestacaoDaProtecaoDoCatalogo repetida = Do(tipo, primeira).Registrar(tipo.Aprovacao, CicloDaAprovacao.Primeiro, Adequada(), Kevin, Instante.AddMinutes(1)).Value!;

        repetida.SubstituiId.Should().Be(primeira.Id);
        tipo.Aprovacao.ManifestadaEm.Should().Be(Instante.AddMinutes(1));
    }

    [Fact(DisplayName = "Toda manifestação muda o circuito, mesmo no mesmo instante e com o mesmo indicador: o ciclo conta cada evento")]
    public void Registrar_MesmoInstanteEMesmoIndicador_AindaMudaOCircuito()
    {
        TipoDocumento tipo = new TipoDocumentoNaAprovacaoBuilder().Solicitado().Build();
        ManifestacaoDaProtecaoDoCatalogo primeira = Do(tipo).Registrar(tipo.Aprovacao, CicloDaAprovacao.Primeiro, Adequada(), Kevin, Instante).Value!;
        (DateTimeOffset?, bool, int) depoisDaPrimeira = (tipo.Aprovacao.ManifestadaEm, tipo.Aprovacao.ManifestacaoDesfavoravelVigente, tipo.Aprovacao.ManifestacoesNoCiclo);

        Do(tipo, primeira).Registrar(tipo.Aprovacao, CicloDaAprovacao.Primeiro, Adequada(), Lucia, Instante);

        (tipo.Aprovacao.ManifestadaEm, tipo.Aprovacao.ManifestacaoDesfavoravelVigente, tipo.Aprovacao.ManifestacoesNoCiclo)
            .Should().NotBe(depoisDaPrimeira).And.Be((Instante, false, 2));
    }

    [Fact(DisplayName = "As manifestações de pessoas diferentes coexistem, e uma desfavorável vigente basta para o indicador")]
    public void Registrar_PessoasDiferentes_Coexistem()
    {
        TipoDocumento tipo = new TipoDocumentoNaAprovacaoBuilder().Solicitado().Build();
        ManifestacaoDaProtecaoDoCatalogo doKevin = Do(tipo).Registrar(tipo.Aprovacao, CicloDaAprovacao.Primeiro, RecomendaAjuste(), Kevin, Instante).Value!;

        ManifestacaoDaProtecaoDoCatalogo daLucia = Do(tipo, doKevin).Registrar(tipo.Aprovacao, CicloDaAprovacao.Primeiro, Adequada(), Lucia, Instante.AddMinutes(1)).Value!;

        daLucia.SubstituiId.Should().BeNull();
        Do(tipo, doKevin, daLucia).Vigentes.Should().Equal(doKevin, daLucia);
        Do(tipo, doKevin, daLucia).HaDesfavoravelVigente.Should().BeTrue();
        tipo.Aprovacao.ManifestacaoDesfavoravelVigente.Should().BeTrue();
    }

    [Fact(DisplayName = "A manifestação com o ciclo que mudou depois de lido é recusada com código próprio, sem mudar nada")]
    public void Registrar_CicloLidoDiferente_RecusaSemMudar()
    {
        TipoDocumento tipo = new TipoDocumentoNaAprovacaoBuilder().Solicitado().Build();

        Result<ManifestacaoDaProtecaoDoCatalogo> registrada = Do(tipo).Registrar(tipo.Aprovacao, new CicloDaAprovacao(2), Inadequada(), Kevin, Instante);

        registrada.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.CicloMudou);
        tipo.Aprovacao.ManifestadaEm.Should().BeNull();
        tipo.Aprovacao.ManifestacaoDesfavoravelVigente.Should().BeFalse();
    }

    [Theory(DisplayName = "O item decidido no ciclo lido recusa a manifestação por conflito; o pendente, por estar fora de solicitação; e o circuito não muda")]
    [InlineData(SituacaoDaAprovacao.Pendente, ProtecaoDoCatalogoErrorCodes.ItemForaDeSolicitacao)]
    [InlineData(SituacaoDaAprovacao.Aprovada, ProtecaoDoCatalogoErrorCodes.DecisaoJaTomada)]
    [InlineData(SituacaoDaAprovacao.AjusteRecomendado, ProtecaoDoCatalogoErrorCodes.DecisaoJaTomada)]
    [InlineData(SituacaoDaAprovacao.Reprovada, ProtecaoDoCatalogoErrorCodes.DecisaoJaTomada)]
    public void Registrar_ForaDeSolicitacao_RecusaSemMudar(SituacaoDaAprovacao situacao, string codigo)
    {
        TipoDocumento tipo = Construir(situacao);

        Result<ManifestacaoDaProtecaoDoCatalogo> registrada = Do(tipo).Registrar(tipo.Aprovacao, CicloDaAprovacao.Primeiro, Inadequada(), Kevin, Instante);

        registrada.Error!.Code.Should().Be(codigo);
        (tipo.Aprovacao.ManifestadaEm, tipo.Aprovacao.ManifestacoesNoCiclo).Should().Be(((DateTimeOffset?)null, 0));
    }

    [Fact(DisplayName = "Os eventos de outro item ou de outro ciclo, e o circuito em outro ciclo, são erro de programação")]
    public void De_EventosOuCircuitoDeOutroCiclo_Falham()
    {
        TipoDocumento tipo = new TipoDocumentoNaAprovacaoBuilder().Solicitado().Build();
        ManifestacaoDaProtecaoDoCatalogo evento = Do(tipo).Registrar(tipo.Aprovacao, CicloDaAprovacao.Primeiro, Adequada(), Kevin, Instante).Value!;

        Action deOutroCiclo = () => ManifestacoesDoCiclo.De(ItemDe(tipo), new CicloDaAprovacao(2), [evento]);
        Action circuitoEmOutroCiclo = () => ManifestacoesDoCiclo.De(ItemDe(tipo), new CicloDaAprovacao(2), [])
            .Registrar(tipo.Aprovacao, new CicloDaAprovacao(2), Adequada(), Kevin, Instante);

        deOutroCiclo.Should().Throw<ArgumentException>();
        circuitoEmOutroCiclo.Should().Throw<InvalidOperationException>();
    }

    private static TipoDocumento Construir(SituacaoDaAprovacao situacao) => situacao switch
    {
        SituacaoDaAprovacao.Aprovada => new TipoDocumentoNaAprovacaoBuilder().Aprovado().Build(),
        SituacaoDaAprovacao.AjusteRecomendado => new TipoDocumentoNaAprovacaoBuilder().ComAjusteRecomendado().Build(),
        SituacaoDaAprovacao.Reprovada => new TipoDocumentoNaAprovacaoBuilder().Reprovado().Build(),
        _ => new TipoDocumentoNaAprovacaoBuilder().Build(),
    };
}
