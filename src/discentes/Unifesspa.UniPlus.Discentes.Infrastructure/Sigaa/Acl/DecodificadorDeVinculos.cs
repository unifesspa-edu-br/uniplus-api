namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Acl;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Traduz o que a API do SIGAA entrega para o domínio do módulo, sem deixar nenhum tipo
/// da origem atravessar a fronteira.
/// </summary>
/// <remarks>
/// <para>
/// Nenhum vínculo estragado interrompe a leitura dos demais: um registro que não pode ser
/// traduzido fica de fora, com o motivo anotado, e a página segue. Uma sincronização de
/// dezenas de milhares de vínculos não pode parar porque um deles veio errado.
/// </para>
/// <para>
/// Os motivos, porém, não são todos iguais, e é por isso que ficam separados. Faltar a
/// unidade acadêmica do curso, ou o período de ingresso, é rotina: o contrato permite, o
/// modelo da réplica exige, e o volume é conhecido. Já faltar um campo que o contrato
/// declara obrigatório é a origem entregando fora do combinado — não deveria acontecer, e
/// quando acontece em massa significa que o contrato mudou sem aviso. Nesse caso a
/// execução termina sem escrever quase nada, e é a contagem separada que impede isso de
/// passar por uma sincronização bem-sucedida.
/// </para>
/// </remarks>
public static class DecodificadorDeVinculos
{
    /// <summary>
    /// Traduz uma página inteira, deixando de fora o que não puder ser traduzido.
    /// </summary>
    public static ResultadoDaDecodificacao Decodificar(IReadOnlyList<VinculoDiscentePayload> pagina)
    {
        ArgumentNullException.ThrowIfNull(pagina);

        List<VinculoDecodificado> aceitos = new(pagina.Count);
        List<VinculoDescartado> descartados = [];

        foreach (VinculoDiscentePayload item in pagina)
        {
            if (Traduzir(item, descartados) is { } vinculo)
            {
                aceitos.Add(vinculo);
            }
        }

        return new ResultadoDaDecodificacao(aceitos, descartados);
    }

    /// <summary>
    /// O vínculo que entrou sem nenhum registro: a própria entrada da página veio nula.
    /// </summary>
    private static readonly VinculoDescartado ItemAusente =
        new(null, MotivoDeDescarte.ForaDoContrato, "vínculo");

    /// <summary>
    /// O que o contrato declara obrigatório, na ordem em que é conferido: o primeiro que
    /// faltar dá nome ao descarte. Ausência aqui é entrega fora do combinado.
    /// </summary>
    private static readonly ObrigatorioDoContrato[] Obrigatorios =
    [
        new("idDiscente", i => i.IdDiscente <= 0),

        // A origem promete matrícula composta só por dígitos. Letra ou pontuação aqui é
        // entrega fora do combinado, não valor que a réplica não comporta.
        new("matricula", i => string.IsNullOrWhiteSpace(i.Matricula) || !MatriculaDoDiscente.ApenasDigitos(i.Matricula)),
        new("nome", i => string.IsNullOrWhiteSpace(i.Nome)),
        new("nivel", i => string.IsNullOrWhiteSpace(i.Nivel)),
        new("cpf", i => string.IsNullOrWhiteSpace(i.Cpf)),
        new("curso", i => i.Curso is null),
        new("curso.id", i => i.Curso is { Id: <= 0 }),
        new("curso.nome", i => string.IsNullOrWhiteSpace(i.Curso?.Nome)),
        new("situacao", i => i.Situacao is null),

        // O contrato declara a situação como conjunto fechado, então aqui não basta ser
        // positiva: um identificador que a origem não declara é um estado que ela própria
        // não trata como vínculo, e guardá-lo faria a réplica afirmar algo que a origem não
        // afirma — com o CPF cifrado junto e contado como sucesso.
        new("situacao.id", i => i.Situacao is { } s && !SituacoesDoContrato.Declarada(s.Id)),
        new("situacao.descricao", i => string.IsNullOrWhiteSpace(i.Situacao?.Descricao)),
    ];

    /// <summary>
    /// Os dois limites de cada campo de texto, na ordem em que são conferidos.
    /// </summary>
    private static readonly LimiteDeTexto[] LimitesDeTexto =
    [
        new("matricula", i => i.Matricula, null, LimitesDaReplica.Matricula),
        new("nivel", i => i.Nivel, null, LimitesDaReplica.Nivel),
        new("nome", i => i.Nome, LimitesDoContrato.Nome, LimitesDaReplica.Nome),
        new("curso.nome", i => i.Curso?.Nome, LimitesDoContrato.NomeDoCurso, LimitesDaReplica.NomeDoCurso),
        new("curso.codigoEmec", i => i.Curso?.CodigoEmec,
            LimitesDoContrato.CodigoEmecDoCurso, LimitesDaReplica.CodigoEmecDoCurso),
        new("curso.unidade.nome", i => i.Curso?.Unidade?.Nome,
            LimitesDoContrato.NomeDaUnidade, LimitesDaReplica.NomeDaUnidade),
        new("situacao.descricao", i => i.Situacao?.Descricao,
            LimitesDoContrato.DescricaoDaSituacao, LimitesDaReplica.DescricaoDaSituacao),
        new("situacao.situacaoVinculo", i => i.Situacao?.SituacaoVinculo,
            LimitesDoContrato.VinculoDaSituacao, LimitesDaReplica.VinculoDaSituacao),
    ];

    /// <summary>
    /// Traduz um vínculo, ou devolve nulo depois de anotar por que ele fica de fora.
    /// </summary>
    /// <remarks>
    /// O formato do CPF é conferido junto das demais exigências do contrato, e não depois
    /// dos descartes por registro incompleto. Adiante, o primeiro descarte encerraria a
    /// tradução deste vínculo e a conferência nunca aconteceria: um CPF corrompido seria
    /// anotado como registro incompleto sempre que viesse acompanhado de um curso sem
    /// unidade — confundindo entrega fora do combinado com rotina, justamente no caso mais
    /// frequente da origem.
    /// </remarks>
    private static VinculoDecodificado? Traduzir(
        VinculoDiscentePayload item,
        List<VinculoDescartado> descartados)
    {
        if (item is null)
        {
            return Descartar(descartados, ItemAusente);
        }

        if (CampoObrigatorioAusente(item) is { } faltando)
        {
            return Descartar(descartados, Descarte(item, MotivoDeDescarte.ForaDoContrato, faltando));
        }

        return Cpf.Criar(item.Cpf).Match(
            cpf => TraduzirComCpf(item, cpf, descartados),
            _ => Descartar(descartados, Descarte(item, MotivoDeDescarte.ForaDoContrato, "cpf")));
    }

    /// <summary>
    /// Segue a tradução de um vínculo cujo CPF já foi aceito: confere os textos, a unidade
    /// do curso e o período de ingresso, nessa ordem, e monta o vínculo.
    /// </summary>
    /// <remarks>
    /// O contrato já exigiu o curso e a situação (<see cref="CampoObrigatorioAusente"/>);
    /// o padrão abaixo apenas os captura, junto da unidade, que ele permite faltar.
    /// </remarks>
    private static VinculoDecodificado? TraduzirComCpf(
        VinculoDiscentePayload item,
        Cpf cpf,
        List<VinculoDescartado> descartados)
    {
        if (TextoAlemDoPermitido(item) is { } excedente)
        {
            return Descartar(descartados, excedente);
        }

        if (item is not { Curso: { Unidade: { } unidade } curso, Situacao: { } situacao })
        {
            return Descartar(descartados, Descarte(item, MotivoDeDescarte.CursoSemUnidadeAcademica));
        }

        return item is { AnoIngresso: { } ano, PeriodoIngresso: { } periodo }
            ? Montar(item, cpf, new PartesDoVinculo(curso, unidade, situacao, ano, periodo), descartados)
            : Descartar(descartados, Descarte(item, MotivoDeDescarte.SemPeriodoDeIngresso));
    }

    /// <summary>
    /// Devolve o primeiro campo de texto que estoura algum dos dois limites, já como
    /// descarte, ou nulo quando todos cabem.
    /// </summary>
    /// <remarks>
    /// Os dois limites dizem coisas diferentes e não podem ser confundidos. Passar do que o
    /// contrato declara é a origem entregando além do que promete, e precisa aparecer como
    /// tal — as colunas são mais largas do que o contrato em quase todo campo, então um
    /// limite só pela coluna deixaria a origem divergir por uma faixa larga sem que nada
    /// apontasse a divergência. Passar do que a coluna comporta, aí sim, é o registro que a
    /// réplica não guarda; sem esta conferência ele só seria recusado na gravação,
    /// derrubando o lote inteiro e levando junto os vínculos válidos que o acompanhassem.
    /// </remarks>
    private static VinculoDescartado? TextoAlemDoPermitido(VinculoDiscentePayload item)
    {
        foreach (LimiteDeTexto limite in LimitesDeTexto)
        {
            if (limite.MotivoDoExcesso(item) is { } motivo)
            {
                return Descarte(item, motivo, limite.Campo);
            }
        }

        return null;
    }

    /// <summary>
    /// Devolve o nome do primeiro campo obrigatório que a origem não entregou, ou nulo
    /// quando todos vieram.
    /// </summary>
    private static string? CampoObrigatorioAusente(VinculoDiscentePayload item) =>
        Obrigatorios.FirstOrDefault(obrigatorio => obrigatorio.Ausente(item)).Campo;

    /// <summary>
    /// Constrói o vínculo a partir das partes que o contrato permite faltar e que a réplica
    /// exige, ou anota qual delas a réplica não aceitou.
    /// </summary>
    private static VinculoDecodificado? Montar(
        VinculoDiscentePayload item,
        Cpf cpf,
        PartesDoVinculo partes,
        List<VinculoDescartado> descartados)
    {
        CursoCopiadoDoSigaa? curso = ValorOuNulo(partes.CriarCurso());
        SituacaoAcademicaCopiada? situacao = ValorOuNulo(partes.CriarSituacao());
        PeriodoIngresso? ingresso = ValorOuNulo(partes.CriarIngresso());

        if (curso is null || situacao is null || ingresso is null)
        {
            return Descartar(
                descartados,
                Descarte(item, MotivoDeDescarte.ForaDoContrato, DescreverParteInvalida(curso, situacao)));
        }

        return CriarVinculo(item, cpf, curso, situacao, ingresso, descartados);
    }

    private static VinculoDecodificado? CriarVinculo(
        VinculoDiscentePayload item,
        Cpf cpf,
        CursoCopiadoDoSigaa curso,
        SituacaoAcademicaCopiada situacao,
        PeriodoIngresso ingresso,
        List<VinculoDescartado> descartados)
    {
        VinculoDiscenteCopiado? dadosCopiados =
            ValorOuNulo(IdentificacaoDoDiscente.Criar(item.IdDiscente, item.Matricula, cpf, item.Nome)) is { } identificacao
                ? ValorOuNulo(VinculoDiscenteCopiado.Criar(identificacao, item.Nivel, curso, situacao, ingresso))
                : null;

        return dadosCopiados is null
            ? Descartar(descartados, Descarte(item, MotivoDeDescarte.ForaDoContrato, "vínculo"))
            : new VinculoDecodificado(VinculoDiscente.Criar(dadosCopiados), ResumirConteudo(dadosCopiados));
    }

    private static string DescreverParteInvalida(
        CursoCopiadoDoSigaa? curso,
        SituacaoAcademicaCopiada? situacao)
    {
        if (curso is null)
        {
            return "curso";
        }

        return situacao is null ? "situacao" : "ingresso";
    }

    /// <summary>
    /// Anota o descarte e devolve nulo, que é o que a tradução devolve quando um vínculo
    /// fica de fora.
    /// </summary>
    private static VinculoDecodificado? Descartar(
        List<VinculoDescartado> descartados,
        VinculoDescartado descarte)
    {
        descartados.Add(descarte);
        return null;
    }

    private static VinculoDescartado Descarte(
        VinculoDiscentePayload item,
        MotivoDeDescarte motivo,
        string? detalhe = null) =>
        new(item.IdDiscente > 0 ? item.IdDiscente : null, motivo, detalhe);

    private static T? ValorOuNulo<T>(Result<T> resultado)
        where T : class =>
        resultado.Match<T?>(valor => valor, _ => null);

    /// <summary>
    /// Resume, num valor curto, o que veio da origem para este vínculo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O resumo é calculado sobre os valores já traduzidos, em ordem fixa, e não sobre o
    /// texto recebido: assim ele não muda quando a origem reordena campos ou altera
    /// espaçamento, e muda sempre que qualquer campo coberto muda.
    /// </para>
    /// <para>
    /// <b>O CPF fica de fora, deliberadamente.</b> O resumo é guardado ao lado dos demais
    /// campos, que ficam legíveis na mesma linha — quem tivesse a tabela conheceria tudo
    /// menos o CPF e poderia testar, um a um, os pouco mais de um bilhão de CPFs válidos
    /// até achar o que reproduz o resumo. Isso é rápido com equipamento comum e devolveria
    /// em claro justamente o dado que a cifra em repouso protege.
    /// </para>
    /// <para>
    /// A consequência é conhecida e aceita: uma correção de CPF na origem que não venha
    /// acompanhada de mudança em nenhum outro campo não é percebida por este resumo, e o
    /// valor antigo permanece na réplica até que algo mais mude naquele vínculo. Cobrir
    /// esse caso exige derivar o resumo com uma chave secreta do módulo — capacidade que o
    /// serviço de criptografia ainda não oferece, e que é assunto da mesma decisão que
    /// trata da busca por CPF.
    /// </para>
    /// </remarks>
    private static ResumoSha256 ResumirConteudo(VinculoDiscenteCopiado dadosCopiados)
    {
        StringBuilder canonico = new();

        foreach (string? campo in CamposDoResumo(dadosCopiados))
        {
            Acrescentar(canonico, campo);
        }

        return ResumoSha256.DoConteudo(Encoding.UTF8.GetBytes(canonico.ToString()));
    }

    /// <summary>
    /// Os campos que entram no resumo, na ordem fixa em que são acrescentados. Mudar a
    /// ordem muda o resumo de todos os vínculos já guardados.
    /// </summary>
    private static IEnumerable<string?> CamposDoResumo(VinculoDiscenteCopiado dadosCopiados) =>
    [
        dadosCopiados.Identificacao.IdDiscenteSigaa.Valor.ToString(CultureInfo.InvariantCulture),
        dadosCopiados.Identificacao.Matricula.Valor,
        dadosCopiados.Identificacao.Nome.Valor,
        dadosCopiados.Nivel.Valor,
        .. CamposDoCurso(dadosCopiados.Curso),
        .. CamposDaSituacao(dadosCopiados.Situacao),
        dadosCopiados.Ingresso.Ano.ToString(CultureInfo.InvariantCulture),
        dadosCopiados.Ingresso.Periodo.ToString(CultureInfo.InvariantCulture),
    ];

    private static IEnumerable<string?> CamposDoCurso(CursoCopiadoDoSigaa curso) =>
    [
        curso.Id.Valor.ToString(CultureInfo.InvariantCulture),
        curso.Nome.Valor,
        curso.CodigoEmec?.Valor,
        curso.Unidade.Id.Valor.ToString(CultureInfo.InvariantCulture),
        curso.Unidade.Nome.Valor,
    ];

    private static IEnumerable<string?> CamposDaSituacao(SituacaoAcademicaCopiada situacao) =>
    [
        situacao.Id.Valor.ToString(CultureInfo.InvariantCulture),
        situacao.Descricao.Valor,
        situacao.Qualificador?.Valor,
    ];

    /// <summary>
    /// Acrescenta um campo à forma canônica, precedido do próprio tamanho.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O tamanho é o que torna a fronteira entre campos inequívoca. Separar os campos por
    /// um caractere qualquer não bastaria: os textos vêm da origem sem tratamento, e um
    /// deles contendo esse mesmo caractere deslocaria a divisão sem mudar o texto
    /// resultante — dois vínculos diferentes produziriam o mesmo resumo, e uma alteração
    /// real na origem passaria por "nada mudou". Com o tamanho à frente, nenhum conteúdo
    /// consegue imitar uma fronteira.
    /// </para>
    /// <para>
    /// Campo ausente é registrado com tamanho negativo, o que o distingue do campo presente
    /// e vazio sem depender de nenhuma marca que o próprio conteúdo pudesse conter.
    /// </para>
    /// </remarks>
    private static void Acrescentar(StringBuilder canonico, string? valor)
    {
        if (valor is null)
        {
            canonico.Append("-1:");
            return;
        }

        canonico.Append(valor.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(valor);
    }

    /// <summary>
    /// Um campo obrigatório pelo contrato e o que o faz faltar.
    /// </summary>
    private readonly record struct ObrigatorioDoContrato(
        string Campo,
        Func<VinculoDiscentePayload, bool> Ausente);

    /// <summary>
    /// Um campo de texto e os dois limites que ele precisa respeitar.
    /// </summary>
    /// <param name="NoContrato">
    /// Nulo quando o contrato não declara limite para o campo; então só o da réplica vale.
    /// </param>
    private readonly record struct LimiteDeTexto(
        string Campo,
        Func<VinculoDiscentePayload, string?> Ler,
        int? NoContrato,
        int NaReplica)
    {
        /// <summary>
        /// O motivo pelo qual o texto do vínculo estoura o limite, ou nulo quando cabe.
        /// </summary>
        public MotivoDeDescarte? MotivoDoExcesso(VinculoDiscentePayload item)
        {
            if (Ler(item) is not { } valor)
            {
                return null;
            }

            if (NoContrato is { } declarado && valor.Length > declarado)
            {
                return MotivoDeDescarte.ForaDoContrato;
            }

            return valor.Length > NaReplica ? MotivoDeDescarte.NaoCabeNaReplica : null;
        }
    }

    /// <summary>
    /// As partes do vínculo que o contrato permite faltar e que a réplica exige, já
    /// conferidas como presentes.
    /// </summary>
    private sealed record PartesDoVinculo(
        CursoPayload Curso,
        UnidadePayload Unidade,
        SituacaoPayload Situacao,
        int Ano,
        int Periodo)
    {
        public Result<CursoCopiadoDoSigaa> CriarCurso() =>
            CursoCopiadoDoSigaa.Criar(Curso.Id, Curso.Nome, Curso.CodigoEmec, Unidade.Id, Unidade.Nome);

        public Result<SituacaoAcademicaCopiada> CriarSituacao() =>
            SituacaoAcademicaCopiada.Criar(Situacao.Id, Situacao.Descricao, Situacao.SituacaoVinculo);

        public Result<PeriodoIngresso> CriarIngresso() => PeriodoIngresso.Criar(Ano, Periodo);
    }
}
