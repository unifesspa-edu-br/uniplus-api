namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.MontagemDoLocalDeOferta;

public sealed class LocalOfertaTests
{
    [Fact(DisplayName = "Criar com dados válidos preenche tipo, campus responsável e referência de cidade")]
    public void Criar_DadosValidos_Preenche()
    {
        Guid campusId = Guid.CreateVersion7();

        Result<LocalOferta> resultado = Criar(d => d with { CampusResponsavelId = campusId, CodigoEmec = "98765" });

        resultado.IsSuccess.Should().BeTrue();
        LocalOferta local = resultado.Value!;
        local.Tipo.Should().Be(TipoLocalOferta.PoloEad);
        local.CampusResponsavelId.Should().Be(campusId);
        local.CidadeCodigoIbge.Should().Be("1504208");
        local.CidadeUf.Should().Be("PA");
        local.CidadeOrigem.Should().Be(ReferenciaCidadeGeo.OrigemGeoApi);
        local.CidadeDisplayAtualizadoEm.Should().Be(Agora);
        local.Endereco.Should().BeNull();
    }

    [Fact(DisplayName = "Criar normaliza a cidade: sem espaço nas pontas, UF em maiúsculas, código e-MEC vazio vira ausente")]
    public void Criar_ValoresComEspacoECaixa_Normaliza()
    {
        LocalOferta local = Existente(d => d with
        {
            CidadeCodigoIbge = " 1504208 ",
            CidadeNome = " Marabá ",
            CidadeUf = " pa ",
            CodigoEmec = "   ",
        });

        local.CidadeCodigoIbge.Should().Be("1504208");
        local.CidadeNome.Should().Be("Marabá");
        local.CidadeUf.Should().Be("PA");
        local.CodigoEmec.Should().BeNull();
    }

    [Fact(DisplayName = "Criar com endereço estruturado coerente persiste o endereço")]
    public void Criar_ComEnderecoCoerente_PersisteEndereco()
    {
        Result<LocalOferta> resultado = Criar(d => d with { Endereco = Endereco() });

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Endereco!.Cep.Should().Be("68507590");
    }

    [Fact(DisplayName = "Criar com endereço de cidade incoerente com a cidade do local falha")]
    public void Criar_EnderecoCidadeIncoerente_Falha()
    {
        Result<LocalOferta> resultado = Criar(d => d with { Endereco = Endereco(cidadeCodigoIbge: "1501402") });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(EnderecoReferenciaErrorCodes.CidadeIncoerente);
    }

    [Fact(DisplayName = "Criar sem campus responsável é válido (FK intra-banco opcional, ADR-0065)")]
    public void Criar_SemCampusResponsavel_Valido()
    {
        Result<LocalOferta> resultado = Criar();

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.CampusResponsavelId.Should().BeNull();
    }

    [Fact(DisplayName = "Criar com tipo Nenhum falha")]
    public void Criar_TipoNenhum_Falha()
    {
        Result<LocalOferta> resultado = Criar(d => d with { Tipo = TipoLocalOferta.Nenhum });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(LocalOfertaErrorCodes.TipoInvalido);
    }

    [Fact(DisplayName = "Criar com referência de cidade malformada falha")]
    public void Criar_CidadeMalformada_Falha()
    {
        Result<LocalOferta> resultado = Criar(d => d with { CidadeCodigoIbge = "ABCDEFG" });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido);
    }

    private static DeclaracaoDoLocalDeOferta SemTipoENemCidade(DeclaracaoDoLocalDeOferta declaracao) =>
        declaracao with { Tipo = TipoLocalOferta.Nenhum, CidadeCodigoIbge = null, CidadeNome = null, CidadeUf = null };

    [Fact(DisplayName = "Tipo Nenhum e cidade ausente não lançam — devolvem as violações acumuladas")]
    public void Criar_TipoNenhumECidadeAusente_NaoLancaEAcumulaAsViolacoes()
    {
        Result<LocalOferta> resultado = Criar(SemTipoENemCidade);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(erro => erro.Field).Should().Equal("tipo", "cidadeCodigoIbge", "cidadeNome", "cidadeUf");
        resultado.Errors[0].Error.Code.Should().Be(LocalOfertaErrorCodes.TipoInvalido);
    }

    [Fact(DisplayName = "Atualizar com tipo inválido acumula com cidade ausente sem mutar o agregado")]
    public void Atualizar_TipoInvalidoECidadeAusente_AcumulaAsViolacoesSemMutar()
    {
        LocalOferta local = Existente();

        Result resultado = local.Atualizar(
            Declaracao(SemTipoENemCidade),
            Agora.AddDays(1));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(4);
        local.Tipo.Should().Be(TipoLocalOferta.PoloEad, "falha de validação não pode mutar o agregado");
        local.CidadeCodigoIbge.Should().Be("1504208");
    }

    [Fact(DisplayName = "Atualizar com declaração válida aplica os campos")]
    public void Atualizar_DeclaracaoValida_AplicaOsCampos()
    {
        LocalOferta local = Existente();
        Guid campusId = Guid.CreateVersion7();

        Result resultado = local.Atualizar(
            Declaracao(d => d with { Tipo = TipoLocalOferta.CampusSede, CampusResponsavelId = campusId, CodigoEmec = " 123 " }),
            Agora.AddDays(1));

        resultado.IsSuccess.Should().BeTrue();
        local.Tipo.Should().Be(TipoLocalOferta.CampusSede);
        local.CampusResponsavelId.Should().Be(campusId);
        local.CodigoEmec.Should().Be("123");
    }

    [Fact(DisplayName = "ValidarDeclaracao aceita declaração válida sem instanciar o agregado")]
    public void ValidarDeclaracao_DeclaracaoValida_Aceita()
    {
        Result resultado = LocalOferta.ValidarDeclaracao(Declaracao(d => d with { Tipo = TipoLocalOferta.CampusSede }));

        resultado.IsSuccess.Should().BeTrue();
    }
}
