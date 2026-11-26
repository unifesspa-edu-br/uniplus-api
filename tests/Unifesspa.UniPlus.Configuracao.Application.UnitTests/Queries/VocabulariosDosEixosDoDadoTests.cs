namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class VocabulariosDosEixosDoDadoTests
{
    [Fact]
    public void Handle_OrigensFato_DevolveParesNaOrdemDeclarada()
    {
        IReadOnlyList<CodigoComRotulo<OrigemFato>> itens = ListarOrigensFatoQueryHandler.Handle(new ListarOrigensFatoQuery());
        itens.Should().Equal(
            CodigoComRotulo<OrigemFato>.De(OrigemFato.Calculado),
            CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado),
            CodigoComRotulo<OrigemFato>.De(OrigemFato.Integracao));
    }

    [Fact]
    public void Handle_FontesValoresFato_DevolveParesNaOrdemDeclarada()
    {
        IReadOnlyList<CodigoComRotulo<FonteValoresFato>> itens = ListarFontesValoresFatoQueryHandler.Handle(new ListarFontesValoresFatoQuery());
        itens.Should().Equal(
            CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Catalogo),
            CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.OpcoesDoProcesso),
            CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Modalidade),
            CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.MunicipiosDoFada),
            CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.UnidadesFederativas),
            CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Municipios),
            CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.TiposDeDeficiencia),
            CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.MunicipiosProvaPresencial));
    }

    [Fact]
    public void Handle_FormatosTexto_DevolveParesNaOrdemDeclarada()
    {
        IReadOnlyList<CodigoComRotulo<FormatoTexto>> itens = ListarFormatosTextoQueryHandler.Handle(new ListarFormatosTextoQuery());
        itens.Should().Equal(
            CodigoComRotulo<FormatoTexto>.De(FormatoTexto.Livre),
            CodigoComRotulo<FormatoTexto>.De(FormatoTexto.Cpf),
            CodigoComRotulo<FormatoTexto>.De(FormatoTexto.Email),
            CodigoComRotulo<FormatoTexto>.De(FormatoTexto.Telefone),
            CodigoComRotulo<FormatoTexto>.De(FormatoTexto.Cep),
            CodigoComRotulo<FormatoTexto>.De(FormatoTexto.NomePessoa));
    }

    [Fact]
    public void Handle_EscoposFato_DevolveParesNaOrdemDeclarada()
    {
        IReadOnlyList<CodigoComRotulo<EscopoFato>> itens = ListarEscoposFatoQueryHandler.Handle(new ListarEscoposFatoQuery());
        itens.Should().Equal(
            CodigoComRotulo<EscopoFato>.De(EscopoFato.Candidato),
            CodigoComRotulo<EscopoFato>.De(EscopoFato.MembroGrupo));
    }

    [Fact]
    public void Handle_ComponentesDeExibicao_DevolveParesNaOrdemDeclarada()
    {
        IReadOnlyList<CodigoComRotulo<ComponenteDeExibicao>> itens = ListarComponentesDeExibicaoQueryHandler.Handle(new ListarComponentesDeExibicaoQuery());
        itens.Should().Equal(
            CodigoComRotulo<ComponenteDeExibicao>.De(ComponenteDeExibicao.Moeda),
            CodigoComRotulo<ComponenteDeExibicao>.De(ComponenteDeExibicao.Situacao));
    }

    [Fact]
    public void Handle_HipotesesLegaisTratamento_DevolveParesNaOrdemDeclarada()
    {
        IReadOnlyList<CodigoComRotulo<HipoteseLegalTratamento>> itens = ListarHipotesesLegaisTratamentoQueryHandler.Handle(new ListarHipotesesLegaisTratamentoQuery());
        itens.Should().Equal(
            CodigoComRotulo<HipoteseLegalTratamento>.De(HipoteseLegalTratamento.Consentimento),
            CodigoComRotulo<HipoteseLegalTratamento>.De(HipoteseLegalTratamento.CumprimentoObrigacaoLegal),
            CodigoComRotulo<HipoteseLegalTratamento>.De(HipoteseLegalTratamento.ExecucaoPoliticasPublicas),
            CodigoComRotulo<HipoteseLegalTratamento>.De(HipoteseLegalTratamento.EstudosPorOrgaoDePesquisa),
            CodigoComRotulo<HipoteseLegalTratamento>.De(HipoteseLegalTratamento.ExecucaoContrato),
            CodigoComRotulo<HipoteseLegalTratamento>.De(HipoteseLegalTratamento.ExercicioRegularDeDireitos),
            CodigoComRotulo<HipoteseLegalTratamento>.De(HipoteseLegalTratamento.ProtecaoDaVida),
            CodigoComRotulo<HipoteseLegalTratamento>.De(HipoteseLegalTratamento.TutelaDaSaude),
            CodigoComRotulo<HipoteseLegalTratamento>.De(HipoteseLegalTratamento.InteresseLegitimo),
            CodigoComRotulo<HipoteseLegalTratamento>.De(HipoteseLegalTratamento.ProtecaoDoCredito),
            CodigoComRotulo<HipoteseLegalTratamento>.De(HipoteseLegalTratamento.PrevencaoAFraude));
    }
}
