namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// O certame como o público o vê, materializado no instante em que ele passa a ser divulgável.
/// </summary>
/// <remarks>
/// <para>
/// <b>A existência desta linha É a publicidade do certame.</b> Ela nasce quando o ato normativo que
/// criou a versão se confirma no registro central, e avança quando o ato de uma retificação se
/// confirma. Não há coluna de "visível": não existir e não ser público são a mesma coisa.
/// </para>
/// <para>
/// É o que torna a leitura pública uma consulta de tabela única. Ordenar por prazo, filtrar por
/// situação, contar por situação e servir o detalhe passam a olhar só aqui — sem resolver linhagem,
/// sem perguntar a outro módulo no caminho da requisição, e sem interpretar o documento congelado a
/// cada leitura.
/// </para>
/// <para>
/// <b>A retificação cujo ato não se confirma não avança a linha</b>, e é isso que mantém o certame
/// no ar com o conteúdo anterior. Publicação é ato público, e torná-la invisível fere a
/// transparência — é para isso que existe retificação de ato, não supressão. Enquanto o ato da
/// retificação não existe, ela não tem publicidade, e o que se serve é o último estado que tem.
/// </para>
/// <para>
/// Derivado, e reconstruível: tudo aqui sai da versão de configuração congelada, que permanece a
/// fonte. Perder esta tabela custa reprojetar, nunca dado.
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
    public string Certame { get; private set; } = null!;

    /// <summary>Instante em que esta divulgação foi materializada.</summary>
    public DateTimeOffset DivulgadoEm { get; private set; }

    public static CertameDivulgado Criar(
        Guid processoSeletivoId,
        int numeroVersao,
        Guid atoCriadorId,
        string hashConfiguracao,
        string versaoProjecao,
        FacetasDoCertameDivulgado facetas,
        string certame,
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
            Nome = facetas.Nome,
            Numero = facetas.Numero,
            ModalidadesOfertadas = facetas.ModalidadesOfertadas,
            InscricoesDe = facetas.InscricoesDe,
            InscricoesAte = facetas.InscricoesAte,
            Certame = certame,
            DivulgadoEm = divulgadoEm,
        };
    }

    /// <summary>
    /// Avança a divulgação para uma versão mais nova. Recusa retroceder: a reentrega da mensagem de
    /// registro é esperada, e uma entrega atrasada do ato de uma versão ANTERIOR não pode desfazer
    /// uma retificação já divulgada.
    /// </summary>
    public bool TentarAvancar(
        int numeroVersao,
        Guid atoCriadorId,
        string hashConfiguracao,
        string versaoProjecao,
        FacetasDoCertameDivulgado facetas,
        string certame,
        DateTimeOffset divulgadoEm)
    {
        ArgumentNullException.ThrowIfNull(facetas);

        if (numeroVersao <= NumeroVersao)
        {
            return false;
        }

        NumeroVersao = numeroVersao;
        AtoCriadorId = atoCriadorId;
        HashConfiguracao = hashConfiguracao;
        VersaoProjecao = versaoProjecao;
        Nome = facetas.Nome;
        Numero = facetas.Numero;
        ModalidadesOfertadas = facetas.ModalidadesOfertadas;
        InscricoesDe = facetas.InscricoesDe;
        InscricoesAte = facetas.InscricoesAte;
        Certame = certame;
        DivulgadoEm = divulgadoEm;
        return true;
    }
}
