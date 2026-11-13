namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using System.Text.Json;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O que as consultas administrativas da inscrição têm em comum nos testes: a inscrição de Fernanda com um fato de cada
/// classe, a execução do handler com os serviços reais e o verificador da persona, e a leitura dos itens da resposta.
/// </summary>
internal static class ConsultaAdministrativaDeTeste
{
    public static readonly Cpf CpfDeErick = Cpf.Criar(PersonasDeTeste.Candidatas.Single(c => c.Nome.StartsWith("Erick", StringComparison.Ordinal)).Cpf).Value!;

    public static readonly string CodigoAcesso = PermissaoSelecao.InscricoesConsultar.Codigo;
    public static readonly string CodigoPublicos = PermissaoSelecao.DadosPublicosConsultar.Codigo;
    public static readonly string CodigoDocumentosPublicos = PermissaoSelecao.DocumentosPublicosConsultar.Codigo;
    public static readonly string CodigoPessoais = PermissaoSelecao.DadosPessoaisConsultar.Codigo;
    public static readonly string CodigoSensiveis = PermissaoSelecao.DadosSensiveisConsultar.Codigo;
    public static readonly string CodigoRevelar = PermissaoSelecao.DadosIdentificadoresRevelar.Codigo;
    public static readonly string CodigoIdentificadores = PermissaoSelecao.DadosIdentificadoresConsultar.Codigo;
    public static readonly string CodigoEmitir = PermissaoSelecao.InscricoesEmitirComprovante.Codigo;

    public static JsonElement Texto(string valor) => JsonSerializer.SerializeToElement(valor);

    public static InscricaoAdministrativaDeTeste DeFernanda(SituacaoInscricao situacao = SituacaoInscricao.Submetida) =>
        new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond, situacao)
            .Fato("CPF", "IDENTIFICADOR", TipoRenderizacao.Texto, Texto(PersonasDeTeste.FernandaDrumond.Valor), ordem: 0, formato: "CPF")
            .Fato("NOME", "PESSOAL", TipoRenderizacao.Texto, Texto(PersonasDeTeste.NomeFernandaDrumond), ordem: 1)
            .Fato("TIPO_DEFICIENCIA", "SENSIVEL", TipoRenderizacao.Texto, Texto("BAIXA_VISAO"), ordem: 2)
            .Exigencia("LAUDO_MEDICO", ClassificacaoDeProtecao.Sensivel)
            .Exigencia("COMPROVANTE_RESIDENCIA", ClassificacaoDeProtecao.Pessoal);

    public static PermissaoSelecao[] Permissoes(bool consultar, bool revelar) =>
    [
        PermissaoSelecao.InscricoesConsultar,
        PermissaoSelecao.DadosPessoaisConsultar,
        .. consultar ? [PermissaoSelecao.DadosIdentificadoresConsultar] : Array.Empty<PermissaoSelecao>(),
        .. revelar ? [PermissaoSelecao.DadosIdentificadoresRevelar] : Array.Empty<PermissaoSelecao>(),
    ];

    public static Task<Result<InscricaoAdministrativaDto>> Consultar(InscricaoAdministrativaDeTeste cenario, DecisoesDeAcessoDeTeste persona) =>
        Executar(cenario.Montar(), persona, cenario.ProcessoId, cenario.InscricaoId);

    public static async Task<InscricaoAdministrativaDto> ConsultarDto(InscricaoAdministrativaDeTeste cenario, DecisoesDeAcessoDeTeste persona) =>
        (await Consultar(cenario, persona).ConfigureAwait(false)).Value!;

    public static Task<Result<InscricaoAdministrativaDto>> Executar(
        InscricaoAdministrativaDeTeste.Mundo mundo, DecisoesDeAcessoDeTeste persona, Guid processoId, Guid inscricaoId) =>
        ObterInscricaoAdministrativaQueryHandler.Handle(
            new ObterInscricaoAdministrativaQuery(processoId, inscricaoId),
            persona.Verificador,
            new ProjetorDaInscricaoAdministrativa(
                persona.Verificador,
                new ProjetorDeItens(persona.Verificador, Substitute.For<IDecisoesDaRequisicao>()),
                new PortaDePermissaoPorPar<PermissaoSelecao>()),
            new LeituraDaInscricaoAdministrativa(mundo.Processos, mundo.Inscricoes, mundo.Documentos, mundo.Codecs),
            CancellationToken.None);

    public static IEnumerable<ItemDeRespostaDto> ItensDe(InscricaoAdministrativaDto dto) => dto.Secoes.SelectMany(static s => s.Itens);

    public static ItemDeRespostaDto ItemDe(InscricaoAdministrativaDto dto, string fatoCodigo) =>
        ItensDe(dto).Single(i => i.FatoCodigo == fatoCodigo);
}
