namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Projeta os blocos públicos do envelope congelado para o contrato do certame.
/// </summary>
/// <remarks>
/// <b>Tipos declarados campo a campo</b>, nunca recorte de subárvore do documento congelado. A
/// distinção não é estilo: um contrato montado por filtro devolve o campo novo por omissão — basta
/// o envelope ganhar um bloco para ele atravessar a fronteira pública sem que ninguém decida. Com
/// a forma declarada, a fronteira é a assinatura do tipo, e o campo novo só passa quando alguém o
/// escreve aqui.
/// <para>
/// Toda extração confere presença, tipo e nulidade antes de usar o valor. Uma forma inesperada
/// recusa a leitura inteira em vez de emitir campo silenciosamente vazio: o certame é documento com
/// efeito jurídico, e meia projeção mente mais que uma recusa.
/// </para>
/// </remarks>
internal static class ProjecaoDoCertamePublicado
{
    /// <summary>
    /// Versão desta projeção pública. Sobe sempre que o formato desta resposta muda — acrescentar,
    /// remover ou renomear campo. O hash da configuração NÃO cobre essa mudança: ele identifica o
    /// envelope congelado, que continua o mesmo quando só a projeção muda, e um cache endereçado
    /// apenas por ele serviria a resposta antiga depois do deploy.
    /// </summary>
    public const string Versao = "9";

    /// <summary>
    /// Forma do documento divulgado — a MESMA do wire, e a usada para lê-lo de volta. A única parte
    /// da resposta que não está no documento é a situação da janela de inscrição, que depende do
    /// instante e é acrescentada na leitura (<see cref="CertamePublicadoDto.Situacao"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// O documento guardado é a resposta pública, e guardá-lo com outra convenção de nomes o faria
    /// divergir do contrato que ele serve: as chaves do jsonb deixariam de casar com as do corpo, e
    /// quem fosse consultar dentro dele — que é a razão de ser jsonb, e não texto — escreveria o
    /// caminho errado. Fixar aqui evita que a convenção dependa de quem chamou o serializador.
    /// </para>
    /// As três exigências cobrem as três formas de faltar. A de parâmetro de construtor recusa o
    /// membro <b>ausente</b> — <c>"tipoProcesso": {}</c> —; a de anotação de nulidade recusa o
    /// membro <b>presente e nulo</b> — <c>"codigo": null</c> —, que a primeira aceita porque o
    /// argumento existe; e <see cref="ArranjoSemElementoNulo"/> recusa o <b>elemento</b> nulo
    /// dentro do arranjo — <c>"recursos": [null]</c> —, que nenhuma das duas alcança. As três valem
    /// em TODO nível do grafo.
    /// <para>
    /// A alternativa seria descer a cada campo de cada tipo aninhado à mão: a forma do contrato
    /// escrita duas vezes, com a segunda cópia envelhecendo a cada campo novo.
    /// </para>
    /// </remarks>
    public static readonly JsonSerializerOptions OpcoesDoDocumento = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        Converters = { new ArranjoSemElementoNulo() },
    };

    /// <summary>
    /// Código único da recusa de leitura do envelope. Vive aqui, e não repetido em cada ponto que
    /// recusa, porque é ele que o mapeador de erros da API associa ao 422.
    /// </summary>
    internal const string CodigoEnvelopeInesperado = "CertamePublicado.EnvelopeInesperado";

    /// <param name="enderecoNoAcervo">
    /// O endereço público de uma chave do acervo. Chega como função, e não como port, para a projeção
    /// continuar pura: o mesmo envelope e o mesmo endereço base produzem sempre a mesma resposta.
    /// </param>
    public static Result<CertamePublicadoDto> Projetar(
        Guid processoSeletivoId,
        AtoDaDivulgacao ato,
        string nome,
        string hashConfiguracao,
        JsonObject envelope,
        Func<string, Uri> enderecoNoAcervo)
    {
        ArgumentNullException.ThrowIfNull(ato);

        Result<IdentidadeDoCertame> identidade = LerIdentidade(envelope, ato);
        if (identidade.IsFailure)
        {
            return Result<CertamePublicadoDto>.Failure(identidade.Error!);
        }

        Result<OfertaDoCertame> oferta = LerOferta(envelope);
        if (oferta.IsFailure)
        {
            return Result<CertamePublicadoDto>.Failure(oferta.Error!);
        }

        Result<ParticipacaoNoCertame> participacao = LerParticipacao(
            envelope, new DocumentosDoAtoNoAcervo(processoSeletivoId, ato.Id, enderecoNoAcervo, Copiados: true));
        if (participacao.IsFailure)
        {
            return Result<CertamePublicadoDto>.Failure(participacao.Error!);
        }

        return Result<CertamePublicadoDto>.Success(Montar(
            new CabecalhoDoCertame(processoSeletivoId, ato.Id, nome, hashConfiguracao, enderecoNoAcervo),
            identidade.Value!,
            oferta.Value!,
            participacao.Value!));
    }

    private static CertamePublicadoDto Montar(
        CabecalhoDoCertame cabecalho,
        IdentidadeDoCertame identidade,
        OfertaDoCertame oferta,
        ParticipacaoNoCertame participacao) => new(
            cabecalho.ProcessoSeletivoId,
            identidade.IdentificadorLegivel,
            cabecalho.AtoId,
            cabecalho.Nome,
            Versao,
            cabecalho.HashConfiguracao,
            identidade.TipoProcesso,
            identidade.Periodo,
            identidade.Localidade,
            identidade.Unidade,
            new DocumentoEditalCertameDto(identidade.DocumentoDoEdital.DocumentoEditalId, identidade.DocumentoDoEdital.HashSha256, cabecalho.EnderecoNoAcervo(identidade.DocumentoDoEdital.Chave)),
            oferta.Ofertas,
            oferta.OpcoesDeCurso,
            oferta.Modalidades,
            oferta.Vagas,
            participacao.Etapas,
            participacao.OrigemCandidatos,
            participacao.Fases,
            participacao.Exigencias,
            participacao.Atendimento,
            participacao.Taxa,
            participacao.Retificacao);

    private static Result<IdentidadeDoCertame> LerIdentidade(JsonObject envelope, AtoDaDivulgacao ato)
    {
        // Obrigatório na projeção, embora o bloco admita nulo no envelope: publicação e sucessão de
        // versão recusam a ausência, então uma versão divulgável sempre o traz. A regra do cadastro
        // é reaplicada ao valor lido porque é ele que a leitura pública usa como chave de busca.
        if (!PrimitivasDaProjecao.TentarTexto(envelope, BlocoPublico("identificadorLegivel"), out string identificadorLegivel)
            || IdentificadorLegivel.Criar(identificadorLegivel).IsFailure)
        {
            return Recusar<IdentidadeDoCertame>("identificador legível");
        }

        if (!PrimitivasDaProjecao.TentarObjeto(envelope, BlocoPublico("tipoProcesso"), out JsonObject? tipoProcessoNode)
            || !LeituraDoCertameEdital.TentarTipoNomeado(tipoProcessoNode, out TipoCatalogadoCertameDto? tipoProcesso))
        {
            return Recusar<IdentidadeDoCertame>("tipo do processo");
        }

        if (!TentarPeriodo(envelope, out PeriodoInscricaoCertameDto? periodo))
        {
            return Recusar<IdentidadeDoCertame>("período de inscrição");
        }

        if (!TentarLocalidade(envelope, out LocalidadeCertameDto? localidade))
        {
            return Recusar<IdentidadeDoCertame>("localidade");
        }

        if (!LeituraDoCertameEdital.TentarUnidadeAdministradora(envelope, out UnidadeAdministradoraCertameDto? unidade))
        {
            return Recusar<IdentidadeDoCertame>("unidade administradora");
        }

        return LeituraDoCertameEdital.TentarEditalNoAcervo(envelope, ato, out EditalNoAcervo? edital)
            ? Result<IdentidadeDoCertame>.Success(new IdentidadeDoCertame(identificadorLegivel, tipoProcesso, periodo, localidade, unidade, edital))
            : Recusar<IdentidadeDoCertame>("documento do edital");
    }

    private static bool TentarPeriodo(JsonObject envelope, [NotNullWhen(true)] out PeriodoInscricaoCertameDto? periodo)
    {
        periodo = null;
        if (!PrimitivasDaProjecao.TentarObjeto(envelope, BlocoPublico("periodo"), out JsonObject? bloco)
            || !PrimitivasDaProjecao.TentarTextoOpcional(bloco, "numero", out string? numero)
            || !PrimitivasDaProjecao.TentarInstante(bloco, "inicio", out DateTimeOffset inicio)
            || !PrimitivasDaProjecao.TentarInstante(bloco, "fim", out DateTimeOffset fim))
        {
            return false;
        }

        periodo = new PeriodoInscricaoCertameDto(numero, inicio, fim);
        return true;
    }

    private static bool TentarLocalidade(JsonObject envelope, [NotNullWhen(true)] out LocalidadeCertameDto? localidade)
    {
        localidade = null;
        if (!PrimitivasDaProjecao.TentarObjeto(envelope, BlocoPublico("localidade"), out JsonObject? bloco)
            || !PrimitivasDaProjecao.TentarTexto(bloco, "codigoIbge", out string codigoIbge)
            || !PrimitivasDaProjecao.TentarTexto(bloco, "nome", out string nome)
            || !PrimitivasDaProjecao.TentarTexto(bloco, "uf", out string uf)
            || !PrimitivasDaProjecao.TentarTexto(bloco, "fusoHorario", out string fusoHorario)
            // O domínio só sabe dizer o nome da zona institucional, a única que a publicação
            // congela; uma zona diferente sairia com o nome errado, e isso é meia projeção.
            || fusoHorario != FusoHorarioDto.Institucional.Id)
        {
            return false;
        }

        localidade = new LocalidadeCertameDto(codigoIbge, nome, uf, FusoHorarioDto.Institucional);
        return true;
    }

    private static Result<OfertaDoCertame> LerOferta(JsonObject envelope)
    {
        if (!LeituraDoCertameVagas.TentarOfertas(envelope, out List<OfertaDeCursoCertameDto>? ofertas))
        {
            return Recusar<OfertaDoCertame>("ofertas");
        }

        if (!LeituraDoCertameVagas.TentarOpcoesDeCurso(envelope, out OpcoesDeCursoCertameDto? opcoesDeCurso))
        {
            return Recusar<OfertaDoCertame>("opções de curso");
        }

        if (!PrimitivasDaProjecao.TentarTextos(envelope, BlocoPublico("modalidadesOfertadas"), out List<string>? modalidades))
        {
            return Recusar<OfertaDoCertame>("modalidades ofertadas");
        }

        return LeituraDoCertameVagas.TentarVagas(envelope, out List<QuadroDeVagasCertameDto>? vagas)
            ? Result<OfertaDoCertame>.Success(new OfertaDoCertame(ofertas, opcoesDeCurso, modalidades, vagas))
            : Recusar<OfertaDoCertame>("quadro de vagas");
    }

    private static Result<ParticipacaoNoCertame> LerParticipacao(JsonObject envelope, DocumentosDoAtoNoAcervo acervo)
    {
        if (!LeituraDoCertameCronograma.TentarEtapas(envelope, out List<EtapaCertameDto>? etapas))
        {
            return Recusar<ParticipacaoNoCertame>("etapas");
        }

        if (!LeituraDoCertameCronograma.TentarCronograma(envelope, out string? origemCandidatos, out List<FaseCronogramaCertameDto>? fases))
        {
            return Recusar<ParticipacaoNoCertame>("cronograma");
        }

        if (!LeituraDoCertameDocumentos.TentarExigencias(envelope, static _ => true, acervo, out List<ExigenciaDocumentalCertameDto>? exigencias))
        {
            return Recusar<ParticipacaoNoCertame>("documentos exigidos");
        }

        if (!LeituraDoCertameDocumentos.TentarAtendimento(envelope, out AtendimentoCertameDto? atendimento))
        {
            return Recusar<ParticipacaoNoCertame>("atendimento especializado");
        }

        if (!LeituraDoCertameEdital.TentarTaxaInscricao(envelope, out TaxaInscricaoCertameDto? taxa))
        {
            return Recusar<ParticipacaoNoCertame>("taxa de inscrição");
        }

        return LeituraDoCertameEdital.TentarRetificacao(envelope, out RetificacaoCertameDto? retificacao)
            ? Result<ParticipacaoNoCertame>.Success(
                new ParticipacaoNoCertame(etapas, origemCandidatos, fases, exigencias, atendimento, taxa, retificacao))
            : Recusar<ParticipacaoNoCertame>("retificação");
    }

    private sealed record CabecalhoDoCertame(
        Guid ProcessoSeletivoId, Guid AtoId, string Nome, string HashConfiguracao, Func<string, Uri> EnderecoNoAcervo);

    private sealed record IdentidadeDoCertame(
        string IdentificadorLegivel,
        TipoCatalogadoCertameDto TipoProcesso,
        PeriodoInscricaoCertameDto Periodo,
        LocalidadeCertameDto Localidade,
        UnidadeAdministradoraCertameDto Unidade,
        EditalNoAcervo DocumentoDoEdital);

    private sealed record OfertaDoCertame(
        List<OfertaDeCursoCertameDto> Ofertas,
        OpcoesDeCursoCertameDto OpcoesDeCurso,
        List<string> Modalidades,
        List<QuadroDeVagasCertameDto> Vagas);

    private sealed record ParticipacaoNoCertame(
        List<EtapaCertameDto> Etapas,
        string OrigemCandidatos,
        List<FaseCronogramaCertameDto> Fases,
        List<ExigenciaDocumentalCertameDto> Exigencias,
        AtendimentoCertameDto Atendimento,
        TaxaInscricaoCertameDto? Taxa,
        RetificacaoCertameDto? Retificacao);

    /// <summary>
    /// Recusa nomeando a parte do certame pelo nome que o CONTRATO PÚBLICO usa, nunca pela chave
    /// interna do envelope congelado. Um chamador anônimo não deve aprender a estrutura interna do
    /// documento pela mensagem de erro — é a mesma disciplina de ausência de oráculo que motivou o
    /// colapso das recusas numa só resposta.
    /// </summary>
    internal static Result<T> Recusar<T>(string parte) =>
        Result<T>.Failure(new DomainError(
            CodigoEnvelopeInesperado,
            $"A configuração publicada não pôde ser lida em '{parte}' — a leitura do certame foi recusada em vez de responder parcialmente."));

    /// <summary>
    /// O nome de um bloco de topo, conferido contra a classificação de exposição
    /// (<see cref="ClassificacaoDosBlocosDoCertame"/>) no momento em que ele é lido.
    /// </summary>
    /// <remarks>
    /// A classificação sozinha só recusa o bloco NOVO — o que nasce no envelope sem categoria. Ela
    /// não impediria ninguém de projetar aqui um bloco já classificado como INTERNO, que é a forma
    /// mais provável de o vazamento acontecer: o bloco existe, tem categoria, e a projeção passa a
    /// lê-lo mesmo assim, sem que verificação alguma acuse. Passar todo acesso de topo por esta
    /// conferência transforma isso em falha dura, que qualquer teste de projeção do certame
    /// alcança.
    /// </remarks>
    internal static string BlocoPublico(string chave) =>
        ClassificacaoDosBlocosDoCertame.Publicados.Contains(chave)
            ? chave
            : throw new InvalidOperationException(
                $"O contrato público do certame não projeta '{chave}': o bloco não está classificado como público.");

}
