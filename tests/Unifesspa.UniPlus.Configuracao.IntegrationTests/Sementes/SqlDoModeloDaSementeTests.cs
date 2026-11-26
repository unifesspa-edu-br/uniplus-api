namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O texto do SQL que grava o modelo de formulário: o modelo só entra quando nenhum modelo tem o mesmo identificador ou
/// o mesmo código e quando cada par termo e versão que o conteúdo cita existe, com a versão do termo e o termo vivo.
/// Não usa banco: o comportamento sobre banco real está em <see cref="SementeSobreBancoOcupadoTests"/>.
/// </summary>
public sealed class SqlDoModeloDaSementeTests
{
    private const string Modelos = "configuracao.modelos_formulario";
    private const string Termos = "configuracao.termos_de_consentimento";
    private const string Versoes = "configuracao.versoes_do_termo_consentimento";

    private static readonly Guid ModeloId = Guid.Parse("7e570003-0000-7000-8000-000000000001");

    private static readonly (Guid TermoId, Guid VersaoId) PrimeiroPar =
        (Guid.Parse("7e570001-0000-7000-8000-0000000000c1"), Guid.Parse("7e570002-0000-7000-8000-0000000000c1"));

    private static readonly (Guid TermoId, Guid VersaoId) SegundoPar =
        (Guid.Parse("7e570001-0000-7000-8000-0000000000c2"), Guid.Parse("7e570002-0000-7000-8000-0000000000c2"));

    private static readonly (Guid TermoId, Guid VersaoId) OutraVersaoDoPrimeiroTermo =
        (PrimeiroPar.TermoId, Guid.Parse("7e570002-0000-7000-8000-0000000000d1"));

    private static readonly (Guid TermoId, Guid VersaoId) OutroTermoComAPrimeiraVersao = (SegundoPar.TermoId, PrimeiroPar.VersaoId);

    private static readonly DeclaracaoDaSemente Declaracao = new(
        "de teste", "semente-de-teste", new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

    private static ComandoDaSemente Habilitacao() =>
        SementePsrMedicina2027.ComandosDosModelos().Single(static c => c.Item!.Codigo == SementePsrMedicina2027.ModeloDeHabilitacao);

    [Fact(DisplayName = "O modelo só é inserido quando nenhum modelo tem o mesmo identificador ou o mesmo código")]
    public void Modelo_ExigeQueIdentificadorECodigoNaoExistam()
    {
        ComandoDaSemente comando = Habilitacao();

        comando.Texto.Should().Contain(
            $"WHERE NOT EXISTS (SELECT 1 FROM {Modelos} WHERE id = '{comando.Item!.Id}'::uuid OR codigo = 'PSR_MEDICINA_2027_HABILITACAO')");
    }

    [Fact(DisplayName = "O modelo só é inserido quando a versão que ele cita existe, é do termo e o termo não foi removido")]
    public void Modelo_ExigeQueOTermoEAVersaoCitadosExistam()
    {
        TermoDaSemente consulta = SementePsrMedicina2027.Termos[2];

        Habilitacao().Texto.Should().Contain(
            $"AND EXISTS (SELECT 1 FROM {Termos} termo JOIN {Versoes} versao ON versao.termo_consentimento_id = termo.id "
            + $"WHERE termo.id = '{consulta.TermoId}'::uuid AND versao.id = '{consulta.VersaoId}'::uuid AND termo.is_deleted = FALSE)");
    }

    [Fact(DisplayName = "Cada modelo da semente exige um par termo e versão por termo que cita, sem repetir o par")]
    public void Modelo_ExigeUmParPorTermoCitado()
    {
        Dictionary<string, int> esperado = new(StringComparer.Ordinal)
        {
            [SementePsrMedicina2027.ModeloDeInscricaoSemProva] = 2,
            [SementePsrMedicina2027.ModeloDeInscricaoComProva] = 2,
            [SementePsrMedicina2027.ModeloDeHabilitacao] = 1,
            [SementeIsencaoDaTaxa.ModeloDeIsencao] = 2,
        };

        Dictionary<string, int> exigido = SementePsrMedicina2027.ComandosDosModelos()
            .Concat(SementeIsencaoDaTaxa.Comandos().Where(static c => c.Item?.Tipo == TipoDoItemDaSemente.Modelo))
            .ToDictionary(static c => c.Item!.Codigo, ParesExigidos, StringComparer.Ordinal);

        exigido.Should().BeEquivalentTo(esperado);
    }

    [Fact(DisplayName = "Os pares exigidos vêm do conteúdo do modelo: dois pares distintos geram uma condição cada")]
    public void Modelo_ComDoisParesDistintos_ExigeCadaUm()
    {
        ComandoDaSemente comando = ComandoDoModeloQueCita(PrimeiroPar, SegundoPar);

        ParesExigidos(comando).Should().Be(2);
        comando.Texto.Should().Contain(CondicaoDoPar(PrimeiroPar)).And.Contain(CondicaoDoPar(SegundoPar));
    }

    [Fact(DisplayName = "Os pares exigidos vêm do conteúdo do modelo: o mesmo termo com duas versões gera uma condição por versão")]
    public void Modelo_ComOMesmoTermoEDuasVersoes_ExigeOParDeCadaVersao()
    {
        ComandoDaSemente comando = ComandoDoModeloQueCita(PrimeiroPar, OutraVersaoDoPrimeiroTermo, PrimeiroPar);

        ParesExigidos(comando).Should().Be(2);
        comando.Texto.Should().Contain(CondicaoDoPar(PrimeiroPar)).And.Contain(CondicaoDoPar(OutraVersaoDoPrimeiroTermo));
    }

    [Fact(DisplayName = "Os pares exigidos vêm do conteúdo do modelo: a mesma versão em dois termos gera uma condição por termo")]
    public void Modelo_ComAMesmaVersaoEmDoisTermos_ExigeOParDeCadaTermo()
    {
        ComandoDaSemente comando = ComandoDoModeloQueCita(PrimeiroPar, OutroTermoComAPrimeiraVersao, OutroTermoComAPrimeiraVersao);

        ParesExigidos(comando).Should().Be(2);
        comando.Texto.Should().Contain(CondicaoDoPar(PrimeiroPar)).And.Contain(CondicaoDoPar(OutroTermoComAPrimeiraVersao));
    }

    [Fact(DisplayName = "Os pares exigidos vêm do conteúdo do modelo: o par repetido gera uma só condição")]
    public void Modelo_ComParRepetido_ExigeOParUmaVez()
    {
        ComandoDaSemente comando = ComandoDoModeloQueCita(PrimeiroPar, PrimeiroPar);

        ParesExigidos(comando).Should().Be(1);
        comando.Texto.Should().Contain(CondicaoDoPar(PrimeiroPar));
    }

    [Fact(DisplayName = "Os pares exigidos vêm do conteúdo do modelo: sem termo, só o identificador e o código livres são exigidos")]
    public void Modelo_SemTermo_ExigeSoIdentificadorECodigoLivres()
    {
        ComandoDaSemente comando = ComandoDoModeloQueCita();

        ParesExigidos(comando).Should().Be(0);
        comando.Texto.Should().NotContain(Termos).And.NotContain(Versoes);
        comando.Texto.Should().Contain(
            $" WHERE NOT EXISTS (SELECT 1 FROM {Modelos} WHERE id = '{ModeloId}'::uuid OR codigo = 'MODELO_DE_TESTE') ON CONFLICT (id) DO NOTHING;");
    }

    [Fact(DisplayName = "O modelo mantém o ON CONFLICT pelo identificador e traz o item que pode pular")]
    public void Modelo_MantemOnConflictETrazOItem()
    {
        ComandoDaSemente comando = Habilitacao();

        comando.Texto.Should().EndWith(" ON CONFLICT (id) DO NOTHING;");
        comando.Item!.Tipo.Should().Be(TipoDoItemDaSemente.Modelo);
    }

    private static int ParesExigidos(ComandoDaSemente comando) => comando.Texto.Split("JOIN " + Versoes, StringSplitOptions.None).Length - 1;

    private static string CondicaoDoPar((Guid TermoId, Guid VersaoId) par) =>
        $"WHERE termo.id = '{par.TermoId}'::uuid AND versao.id = '{par.VersaoId}'::uuid AND termo.is_deleted = FALSE)";

    private static ConteudoDoModelo ConteudoQueCita((Guid TermoId, Guid VersaoId)[] pares) => new(
        "Habilitação de teste",
        [
            new PaginaDoModelo("DADOS", 0, TipoPaginaFormulario.Secao, null, "Dados", null, null, null),
            new PaginaDoModelo("REVISAO", 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null),
        ],
        [new ItemDoModelo("QUILOMBOLA", 0, "DADOS", new ApresentacaoDoCampo("Quilombola", TipoDeCampo.SimOuNao), null, new RegrasDoCampo(null, Obrigatoriedade.Sempre, []))],
        [.. pares.Select(static (par, i) => new TermoDoModelo($"TERMO_{i}", i, par.TermoId, par.VersaoId, new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Sempre)))],
        [],
        []);

    private static ComandoDaSemente ComandoDoModeloQueCita(params (Guid TermoId, Guid VersaoId)[] pares)
    {
        ModeloFormulario modelo = Declaracao.Exigir(
            ModeloFormulario.Criar(
                "MODELO_DE_TESTE", "Modelo de teste", null, new DeclaracaoDoFormulario(ServeAFaseDeInscricao: false, Obrigatorio: true), null, ConteudoQueCita(pares),
                new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)),
            "modelo de teste");
        return SqlDaSemente.ComandoDoModelo(ModeloId, modelo, Declaracao);
    }
}
