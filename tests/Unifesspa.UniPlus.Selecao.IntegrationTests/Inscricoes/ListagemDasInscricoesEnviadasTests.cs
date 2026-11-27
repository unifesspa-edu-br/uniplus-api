namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDaInscricaoGravada;

/// <summary>A listagem das inscrições enviadas do processo lê o nome do candidato só quando quem lista pede o nome.</summary>
[Trait("Category", "Integration")]
[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class ListagemDasInscricoesEnviadasTests
{
    private readonly CenarioDaInscricaoGravada _cenario;

    public ListagemDasInscricoesEnviadasTests(InscricaoDbFixture fixture) => _cenario = new CenarioDaInscricaoGravada(fixture);

    [Fact(DisplayName = "A listagem lê o nome social de quem o usa no lugar do civil, e não lê nome algum quando o nome não é pedido")]
    public async Task ListarEnviadas_LeONomeSocialNoLugarDoCivilSoQuandoPedido()
    {
        Guid processo = await GravarEnvioDeQuemUsaNomeSocialAsync();

        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        InscricaoRepository repositorio = _cenario.Repositorio(contexto);

        (IReadOnlyList<ResumoDaInscricaoDoProcesso> comNome, _, _) = await repositorio.ListarEnviadasDoProcessoAsync(processo, null, null, 10, PaginationDirection.Next, new DadosPessoaisLidosNaListagem(Nome: true, Cpf: false));
        (IReadOnlyList<ResumoDaInscricaoDoProcesso> semNome, _, _) = await repositorio.ListarEnviadasDoProcessoAsync(processo, null, null, 10, PaginationDirection.Next, new DadosPessoaisLidosNaListagem(Nome: false, Cpf: false));

        comNome.Single().Nome.Should().Be("Ariel Drumond");
        semNome.Single().Nome.Should().BeNull();
    }

    private static RespostasAdmitidas AdmitidasComNomeCivilESocial() =>
        new(
            new Dictionary<string, DefinicaoDaResposta>(DefinicoesDasRespostas, StringComparer.Ordinal)
            {
                ["NOME"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false),
            },
            new HashSet<string>(StringComparer.Ordinal) { "NOME", "NOME_SOCIAL", "DESEJA_NOME_SOCIAL" },
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal),
            new HashSet<Guid> { OfertaDoProcesso },
            new Dictionary<TermoNoFormulario, TermoAdmitido>(),
            NomeCivilDoLogin: PersonasDeTeste.NomeFernandaDrumond,
            NomeSocialDoLogin: "Ariel Drumond");

    private async Task<Guid> GravarEnvioDeQuemUsaNomeSocialAsync()
    {
        Guid processo = Guid.CreateVersion7();
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, processo);
        Result<ComprovanteInscricao> envio = Enviar(original, "000654", new ConteudoDoEnvio
        {
            Respostas = Respostas(("NOME", PersonasDeTeste.NomeFernandaDrumond), ("DESEJA_NOME_SOCIAL", true), ("NOME_SOCIAL", "Ariel Drumond")),
            Admitidas = AdmitidasComNomeCivilESocial(),
            Derivados = Respostas(("FAIXA_ETARIA", 25)),
        });
        envio.IsSuccess.Should().BeTrue(string.Join("; ", envio.Errors.Select(static e => e.Field + ":" + e.Error.Code)));
        await _cenario.GravarAsync(original);

        return processo;
    }
}
