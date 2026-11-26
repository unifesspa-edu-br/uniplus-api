namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class InscricaoCopiaDefensivaTests
{
    [Fact]
    public void Reidratar_ListaDeAceitesAlterada_PreservaAceitesDaInscricao()
    {
        List<AceiteDeTermo> aceites = [new(FormularioDeTeste.CodigoDaInscricao, "TERMO", Guid.NewGuid(), "hash")];
        Inscricao inscricao = Inscricao.Reidratar(Guid.NewGuid(), Guid.NewGuid(),
            TitularDaInscricao.Reidratar(PersonasDeTeste.FernandaDrumond, "conta-1", "https://issuer"),
            EstadoDaInscricao.Reidratar(SituacaoInscricao.Rascunho, null, CancelamentoDaInscricao.Reidratar(null, null, null)),
            FormularioDaInscricao.Reidratar(Guid.NewGuid(), PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(null, null), new Dictionary<string, JsonElement>(), new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), aceites),
                new Dictionary<string, JsonElement>(), new Dictionary<string, DefinicaoDaResposta>()), 1, new Unifesspa.UniPlus.Selecao.Domain.ValueObjects.DatasDeGravacaoDaInscricao(default, null));

        aceites.Clear();

        inscricao.Formulario.Preenchimento.Aceites.Should().ContainSingle();
    }
}
