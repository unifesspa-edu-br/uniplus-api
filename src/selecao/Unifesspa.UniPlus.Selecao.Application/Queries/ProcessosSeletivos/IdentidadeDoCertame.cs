namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json.Nodes;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Quem o certame é para o público: o endereço, o tipo do processo, o período de inscrição, a
/// localidade, a unidade administradora e o documento do edital, como o envelope congelado os guarda.
/// </summary>
internal sealed record IdentidadeDoCertame(
    string Nome,
    string IdentificadorLegivel,
    TipoCatalogadoCertameDto TipoProcesso,
    PeriodoInscricaoCertameDto Periodo,
    LocalidadeCertameDto Localidade,
    UnidadeAdministradoraCertameDto Unidade,
    EditalNoAcervo DocumentoDoEdital)
{
    /// <summary>Lê a identidade do envelope, parando na primeira parte que não se deixa ler.</summary>
    internal static Result<IdentidadeDoCertame> Ler(JsonObject envelope, AtoDaDivulgacao ato) =>
        LerNome(envelope).Encadear(nome =>
        LerIdentificadorLegivel(envelope).Encadear(identificadorLegivel =>
            LerTipoDoProcesso(envelope).Encadear(tipoProcesso =>
            LerPeriodo(envelope).Encadear(periodo =>
            LerLocalidade(envelope).Encadear(localidade =>
            LerUnidadeAdministradora(envelope).Encadear(unidade =>
            LerDocumentoDoEdital(envelope, ato).Compor(documentoDoEdital => new IdentidadeDoCertame(
                nome, identificadorLegivel, tipoProcesso, periodo, localidade, unidade, documentoDoEdital))))))));

    /// <remarks>
    /// Obrigatório na projeção, embora o bloco admita nulo no envelope: publicação e sucessão de
    /// versão recusam a ausência, então uma versão divulgável sempre o traz. A regra do cadastro é
    /// reaplicada ao valor lido porque é ele que a leitura pública usa como chave de busca.
    /// </remarks>
    private static Result<string> LerNome(JsonObject envelope) =>
        PrimitivasDaProjecao.TentarTexto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("nome"), out string nome)
        && !string.IsNullOrWhiteSpace(nome) && nome.Length <= 300
            ? Result<string>.Success(nome)
            : ProjecaoDoCertamePublicado.Recusar<string>("nome do processo");

    private static Result<string> LerIdentificadorLegivel(JsonObject envelope) =>
        PrimitivasDaProjecao.TentarTexto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("identificadorLegivel"), out string identificadorLegivel)
        && Domain.ValueObjects.IdentificadorLegivel.Criar(identificadorLegivel).IsSuccess
            ? Result<string>.Success(identificadorLegivel)
            : ProjecaoDoCertamePublicado.Recusar<string>("identificador legível");

    private static Result<TipoCatalogadoCertameDto> LerTipoDoProcesso(JsonObject envelope) =>
        PrimitivasDaProjecao.TentarObjeto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("tipoProcesso"), out JsonObject? bloco)
        && LeituraDoCertameEdital.TentarTipoNomeado(bloco, out TipoCatalogadoCertameDto? tipoProcesso)
            ? Result<TipoCatalogadoCertameDto>.Success(tipoProcesso)
            : ProjecaoDoCertamePublicado.Recusar<TipoCatalogadoCertameDto>("tipo do processo");

    private static Result<PeriodoInscricaoCertameDto> LerPeriodo(JsonObject envelope) =>
        PrimitivasDaProjecao.TentarObjeto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("periodo"), out JsonObject? bloco)
        && PrimitivasDaProjecao.TentarTextoOpcional(bloco, "numero", out string? numero)
        && PrimitivasDaProjecao.TentarInstante(bloco, "inicio", out DateTimeOffset inicio)
        && PrimitivasDaProjecao.TentarInstante(bloco, "fim", out DateTimeOffset fim)
            ? Result<PeriodoInscricaoCertameDto>.Success(new PeriodoInscricaoCertameDto(numero, inicio, fim))
            : ProjecaoDoCertamePublicado.Recusar<PeriodoInscricaoCertameDto>("período de inscrição");

    /// <remarks>
    /// O domínio só sabe dizer o nome da zona institucional, a única que a publicação congela; uma
    /// zona diferente sairia com o nome errado, e isso é meia projeção.
    /// </remarks>
    private static Result<LocalidadeCertameDto> LerLocalidade(JsonObject envelope) =>
        PrimitivasDaProjecao.TentarObjeto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("localidade"), out JsonObject? bloco)
        && PrimitivasDaProjecao.TentarTexto(bloco, "codigoIbge", out string codigoIbge)
        && PrimitivasDaProjecao.TentarTexto(bloco, "nome", out string nome)
        && PrimitivasDaProjecao.TentarTexto(bloco, "uf", out string uf)
        && PrimitivasDaProjecao.TentarTexto(bloco, "fusoHorario", out string fusoHorario)
        && fusoHorario == FusoHorarioDto.Institucional.Id
            ? Result<LocalidadeCertameDto>.Success(new LocalidadeCertameDto(codigoIbge, nome, uf, FusoHorarioDto.Institucional))
            : ProjecaoDoCertamePublicado.Recusar<LocalidadeCertameDto>("localidade");

    private static Result<UnidadeAdministradoraCertameDto> LerUnidadeAdministradora(JsonObject envelope) =>
        LeituraDoCertameEdital.TentarUnidadeAdministradora(envelope, out UnidadeAdministradoraCertameDto? unidade)
            ? Result<UnidadeAdministradoraCertameDto>.Success(unidade)
            : ProjecaoDoCertamePublicado.Recusar<UnidadeAdministradoraCertameDto>("unidade administradora");

    private static Result<EditalNoAcervo> LerDocumentoDoEdital(JsonObject envelope, AtoDaDivulgacao ato) =>
        LeituraDoCertameEdital.TentarEditalNoAcervo(envelope, ato, out EditalNoAcervo? edital)
            ? Result<EditalNoAcervo>.Success(edital)
            : ProjecaoDoCertamePublicado.Recusar<EditalNoAcervo>("documento do edital");
}
