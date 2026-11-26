namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal sealed class CidadeDeProvaBuilder
{
    private readonly ProcessoSeletivo _processo = ProcessoSeletivo.Criar(
        "Processo de seleção", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    internal CidadeDeProvaBuilder(bool comFormularioDeInscricao = true)
    {
        if (comFormularioDeInscricao)
        {
            _processo.DefinirItensComFaseDeInscricao([]).IsSuccess.Should().BeTrue();
        }
    }

    internal CidadeDeProvaBuilder ComDeclaracao(bool temProva)
    {
        _processo.DefinirProvaPresencial(temProva, temProva ? [new("1504208", "PA")] : [], 100).IsSuccess.Should().BeTrue();
        return this;
    }

    internal CidadeDeProvaBuilder ComCampo(Obrigatoriedade obrigatoriedade, PredicadoDnf? exibicao = null)
    {
        FatoColetado campo = FatoColetadoDeTeste.Coletando("CIDADE_DE_PROVA", FormularioDeTeste.PrimeiraOrdemDeInscricao)
            .ComRotulo("Cidade de prova")
            .DoTipo(TipoDeCampo.EscolhaUnica)
            .ComObrigatoriedade(obrigatoriedade)
            .ExibidoQuando(exibicao)
            .ComOrigem(OrigemValoresColeta.MunicipiosDaProvaPresencial)
            .NaSecao(FormularioDeTeste.Secao)
            .Construir();
        _processo.DefinirItens([campo]).IsSuccess.Should().BeTrue();
        return this;
    }

    internal ProcessoSeletivo Construir() => _processo;
}
