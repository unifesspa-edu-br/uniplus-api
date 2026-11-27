namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using Microsoft.AspNetCore.Http;

/// <summary>
/// As respostas de erro que a fronteira HTTP emite sozinha, sem passar por um <c>DomainError</c>:
/// o middleware de exceção, a camada de autenticação, a negociação de versão de mídia, a recusa
/// de contrato dos controllers e o caminho que nenhuma operação atende. Cada emissor lê o status, o código e o título daqui, e o
/// catálogo público de códigos de erro lê o mesmo conjunto por
/// <see cref="ErrosDaFronteiraHttpDomainErrorRegistration"/>.
/// </summary>
public static class ErrosDaFronteiraHttp
{
    public static readonly DomainErrorMapping NaoAutenticado = new(
        StatusCodes.Status401Unauthorized, "uniplus.auth.unauthorized", "Não autenticado");

    public static readonly DomainErrorMapping AcessoNegado = new(
        StatusCodes.Status403Forbidden, "uniplus.auth.forbidden", "Acesso negado");

    public static readonly DomainErrorMapping VersaoDeMidiaNaoSuportada = new(
        StatusCodes.Status406NotAcceptable, "uniplus.contract.versao_nao_suportada", "Versão de mídia não suportada");

    public static readonly DomainErrorMapping IdDaRotaDivergenteDoCorpo = new(
        StatusCodes.Status400BadRequest, "uniplus.contract.id_path_body_divergente", "Id da rota diferente do Id do corpo");

    public static readonly DomainErrorMapping ErroDeDominioNaoMapeado = new(
        StatusCodes.Status400BadRequest, "uniplus.erro_nao_mapeado", "Erro de domínio");

    public static readonly DomainErrorMapping ConflitoDeConcorrencia = new(
        StatusCodes.Status409Conflict, "uniplus.concorrencia.conflito", "Conflito de concorrência");

    public static readonly DomainErrorMapping CursorInvalido = new(
        StatusCodes.Status400BadRequest, "uniplus.paginacao.cursor_invalido", "Cursor inválido");

    public static readonly DomainErrorMapping RegistroDeAcessoNaoGravado = new(
        StatusCodes.Status503ServiceUnavailable,
        "uniplus.autorizacao.registro_de_acesso.falha_ao_gravar",
        "Registro de acesso indisponível");

    public static readonly DomainErrorMapping CatalogoDePermissoesDesatualizado = new(
        StatusCodes.Status503ServiceUnavailable, "uniplus.autorizacao.catalogo.desatualizado", "Sistema em atualização");

    public static readonly DomainErrorMapping EsperaPelaTravaEsgotada = new(
        StatusCodes.Status503ServiceUnavailable, "uniplus.concorrencia.espera_esgotada", "Processo ocupado por outra operação");

    public static readonly DomainErrorMapping CaminhoInexistente = new(
        StatusCodes.Status404NotFound, "uniplus.requisicao.caminho_inexistente", "A requisição pede um caminho que a API não oferece");

    public static readonly DomainErrorMapping ErroInesperado = new(
        StatusCodes.Status500InternalServerError, "uniplus.internal.unexpected", "Erro interno do servidor");
}
