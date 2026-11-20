namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using DTOs;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
    public static readonly JsonSerializerOptions OpcoesDoDocumento = OpcoesJsonDeDocumento.Criar(new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        Converters = { new ArranjoSemElementoNulo() },
    });

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

        DocumentosDoAtoNoAcervo acervo = new(processoSeletivoId, ato.Id, enderecoNoAcervo, Copiados: true);
        return IdentidadeDoCertame.Ler(envelope, ato).Encadear(identidade =>
            OfertaDoCertame.Ler(envelope).Encadear(oferta =>
            ParticipacaoNoCertame.Ler(envelope, acervo).Encadear(participacao =>
            LerRetificacao(envelope).Compor(retificacao => new CertamePublicadoDto(
                processoSeletivoId, identidade.IdentificadorLegivel, ato.Id, nome, Versao, hashConfiguracao,
                identidade.TipoProcesso, identidade.Periodo, identidade.Localidade, identidade.Unidade,
                identidade.DocumentoDoEdital.NoContratoPublico(enderecoNoAcervo),
                oferta.Ofertas, oferta.OpcoesDeCurso, oferta.Modalidades, oferta.Vagas,
                participacao.Etapas, participacao.OrigemCandidatos, participacao.Fases,
                participacao.Exigencias, participacao.Atendimento, participacao.Taxa, retificacao)))));
    }

    private static Result<RetificacaoCertameDto?> LerRetificacao(JsonObject envelope) =>
        LeituraDoCertameEdital.TentarRetificacao(envelope, out RetificacaoCertameDto? retificacao)
            ? Result<RetificacaoCertameDto?>.Success(retificacao)
            : Recusar<RetificacaoCertameDto?>("retificação");

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
