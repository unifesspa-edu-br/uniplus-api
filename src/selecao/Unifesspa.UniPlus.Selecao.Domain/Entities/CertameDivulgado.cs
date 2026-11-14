namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O certame como o público o vê, materializado no instante em que ele passa a ser divulgável.
/// </summary>
/// <remarks>
/// <para>
/// <b>A existência desta linha É a publicidade do certame.</b> Ela nasce na transação que publica o
/// edital de abertura, junto da versão e do ato, e avança na que publica a retificação. Não há coluna
/// de "visível": não existir e não ser público são a mesma coisa. É o que torna a leitura pública uma
/// consulta de tabela única.
/// </para>
/// <para>
/// <b>O endereço dos documentos de cada ato chega depois</b>, quando a cópia ao acervo termina: até lá a
/// entrada do ato existe e não tem endereço.
/// </para>
/// <para>
/// Derivado e reconstruível: tudo sai da versão de configuração congelada, que permanece a fonte.
/// </para>
/// </remarks>
public sealed class CertameDivulgado : IIdentificavel
{
    private CertameDivulgado()
    {
    }

    /// <summary>O processo, que é também a identidade da linha: um certame, uma divulgação.</summary>
    public Guid Id { get; private init; }

    /// <summary>Versão de configuração que esta divulgação projeta.</summary>
    public int NumeroVersao { get; private set; }

    /// <summary>Ato normativo que criou a versão projetada — o que confirmou a publicidade.</summary>
    public Guid AtoCriadorId { get; private set; }

    /// <summary>
    /// Selo do conteúdo: resumo da configuração projetada, com a versão do formato desta projeção.
    /// </summary>
    public string HashConfiguracao { get; private set; } = null!;

    /// <summary>Versão do formato da projeção pública. Sobe quando a forma da resposta muda.</summary>
    public string VersaoProjecao { get; private set; } = null!;

    /// <summary>
    /// Identificador legível congelado na versão projetada. Coluna, com índice único, porque a
    /// leitura pública localiza o certame por ele.
    /// </summary>
    public string IdentificadorLegivel { get; private set; } = null!;

    /// <summary>
    /// Título congelado do certame. Coluna, e não só campo do documento, porque a vitrine o busca e
    /// ordena por ele.
    /// </summary>
    public string Nome { get; private set; } = null!;

    /// <summary>Identificador legível do edital, quando a publicação o declara. A busca o alcança.</summary>
    public string? Numero { get; private set; }

    /// <summary>
    /// Códigos das modalidades com vaga no certame — o recorte por modalidade da vitrine corre
    /// sobre eles.
    /// </summary>
    public IReadOnlyList<string> ModalidadesOfertadas { get; private set; } = [];

    /// <summary>Abertura da janela de inscrição da versão projetada.</summary>
    public DateTimeOffset InscricoesDe { get; private set; }

    /// <summary>
    /// Encerramento da janela da versão projetada — a chave por que a vitrine ordena por padrão, e a
    /// grandeza de que as quatro situações derivam, junto com a abertura.
    /// </summary>
    public DateTimeOffset InscricoesAte { get; private set; }

    /// <summary>
    /// A resposta pública já pronta. Guardar o documento projetado, e não os campos em colunas,
    /// evita uma coluna nova a cada campo que o contrato ganhe — e a forma continua declarada, pelo
    /// tipo que a projetou.
    /// </summary>
    public string DocumentoPublico { get; private set; } = null!;

    /// <summary>
    /// Os documentos de cada ato divulgado do processo, identificados pelo ato: o edital de cada versão,
    /// pelo ato que a criou, e o documento de cada ato que não cria versão. Fica fora do documento
    /// <see cref="DocumentoPublico"/> — que descreve só a versão vigente — e cresce a cada divulgação.
    /// </summary>
    public IReadOnlyList<AtoDivulgado> AtosDivulgados { get; private set; } = [];

    /// <summary>Instante em que esta divulgação foi materializada.</summary>
    public DateTimeOffset DivulgadoEm { get; private set; }

    public static CertameDivulgado Criar(
        Guid processoSeletivoId,
        int numeroVersao,
        Guid atoCriadorId,
        string hashConfiguracao,
        string versaoProjecao,
        FacetasDoCertameDivulgado facetas,
        string documentoPublico,
        DateTimeOffset divulgadoEm)
    {
        ArgumentNullException.ThrowIfNull(facetas);

        return new()
        {
            Id = processoSeletivoId,
            NumeroVersao = numeroVersao,
            AtoCriadorId = atoCriadorId,
            HashConfiguracao = hashConfiguracao,
            VersaoProjecao = versaoProjecao,
            IdentificadorLegivel = facetas.IdentificadorLegivel,
            Nome = facetas.Nome,
            Numero = facetas.Numero,
            // Cópia: o tipo do parâmetro é só leitura, mas a instância concreta é do chamador, e
            // uma mutação depois desta linha alteraria a coluna sem passar pela projeção — que é
            // a divergência entre faceta e documento que a materialização existe para impedir.
            ModalidadesOfertadas = [.. facetas.ModalidadesOfertadas],
            InscricoesDe = facetas.InscricoesDe,
            InscricoesAte = facetas.InscricoesAte,
            DocumentoPublico = documentoPublico,
            DivulgadoEm = divulgadoEm,
        };
    }

    /// <summary>
    /// Avança a divulgação para uma versão mais nova. A versão que não é mais nova é erro de programação: o
    /// comando que publica trava o processo antes de ler a divulgação, então a cadeia de versões é linear.
    /// </summary>
    public void Avancar(
        int numeroVersao,
        Guid atoCriadorId,
        string hashConfiguracao,
        string versaoProjecao,
        FacetasDoCertameDivulgado facetas,
        string documentoPublico,
        DateTimeOffset divulgadoEm)
    {
        ArgumentNullException.ThrowIfNull(facetas);

        if (numeroVersao <= NumeroVersao)
        {
            throw new InvalidOperationException(
                $"A divulgação do processo {Id} está na versão {NumeroVersao} e não avança para a versão {numeroVersao}.");
        }

        NumeroVersao = numeroVersao;
        AtoCriadorId = atoCriadorId;
        HashConfiguracao = hashConfiguracao;
        VersaoProjecao = versaoProjecao;
        IdentificadorLegivel = facetas.IdentificadorLegivel;
        Nome = facetas.Nome;
        Numero = facetas.Numero;
        ModalidadesOfertadas = [.. facetas.ModalidadesOfertadas];
        InscricoesDe = facetas.InscricoesDe;
        InscricoesAte = facetas.InscricoesAte;
        DocumentoPublico = documentoPublico;
        DivulgadoEm = divulgadoEm;
    }

    /// <summary>
    /// Registra a entrada do ato que o comando acabou de divulgar, ainda sem endereço. Um ato divulga uma
    /// vez: repetir a entrada é erro de programação.
    /// </summary>
    public void AbrirEntradaDoAto(Guid atoId, int? numeroVersao)
    {
        if (AtosDivulgados.Any(e => e.AtoId == atoId))
        {
            throw new InvalidOperationException($"O ato {atoId} já tem entrada na divulgação do processo {Id}.");
        }

        // Lista nova, e não mutação da atual: o rastreador detecta a mudança por comparação do
        // valor, e a instância antiga pode estar compartilhada com quem a leu.
        AtosDivulgados = [.. AtosDivulgados, AtoDivulgado.SemEndereco(atoId, numeroVersao)];
    }

    /// <summary>
    /// Registra o endereço e os documentos do ato depois da cópia ao acervo. Idempotente por ato: a reentrega da
    /// cópia não muda a entrada que já tem endereço, e a cópia de um ato mais antigo não toca na de nenhum outro.
    /// </summary>
    /// <returns><see langword="true"/> quando a lista mudou.</returns>
    public bool RegistrarEnderecos(Guid atoId, string urlDownload, IReadOnlyList<DocumentoDivulgado> documentos)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(urlDownload);
        ArgumentNullException.ThrowIfNull(documentos);

        if (EntradaDoAto(atoId).UrlDownload is not null)
        {
            return false;
        }

        AtosDivulgados = [.. AtosDivulgados.Select(e => e.AtoId == atoId ? e with { UrlDownload = urlDownload, Documentos = documentos } : e)];

        return true;
    }

    private AtoDivulgado EntradaDoAto(Guid atoId) =>
        AtosDivulgados.SingleOrDefault(e => e.AtoId == atoId)
        ?? throw new InvalidOperationException($"O ato {atoId} não tem entrada na divulgação do processo {Id}.");

    /// <summary>Se a cópia ao acervo dos documentos do ato já registrou o endereço.</summary>
    public bool CopiouOsDocumentosDoAto(Guid atoId) =>
        AtosDivulgados.Any(e => e.AtoId == atoId && e.UrlDownload is not null);
}
