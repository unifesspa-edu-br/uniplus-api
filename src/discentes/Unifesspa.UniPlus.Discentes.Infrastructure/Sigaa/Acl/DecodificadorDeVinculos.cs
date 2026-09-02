namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Acl;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

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
    /// Traduz um vínculo, ou devolve nulo depois de anotar por que ele fica de fora.
    /// </summary>
    private static VinculoDecodificado? Traduzir(
        VinculoDiscentePayload item,
        List<VinculoDescartado> descartados)
    {
        if (item is null)
        {
            descartados.Add(new VinculoDescartado(null, MotivoDeDescarte.ForaDoContrato, "vínculo"));
            return null;
        }

        // O que o contrato declara obrigatório. Ausência aqui é entrega fora do combinado.
        long? identificador = item.IdDiscente > 0 ? item.IdDiscente : null;

        if (CampoObrigatorioAusente(item) is { } faltando)
        {
            descartados.Add(new VinculoDescartado(
                identificador, MotivoDeDescarte.ForaDoContrato, faltando));
            return null;
        }

        // O formato do CPF é conferido junto das demais exigências do contrato, e não
        // depois dos descartes por registro incompleto. Adiante, o primeiro descarte
        // encerraria a tradução deste vínculo e a conferência nunca aconteceria: um CPF
        // corrompido seria anotado como registro incompleto sempre que viesse acompanhado
        // de um curso sem unidade — confundindo entrega fora do combinado com rotina,
        // justamente no caso mais frequente da origem.
        Cpf? cpf = Cpf.Criar(item.Cpf).Match<Cpf?>(valido => valido, _ => null);
        if (cpf is null)
        {
            descartados.Add(new VinculoDescartado(
                identificador, MotivoDeDescarte.ForaDoContrato, "cpf"));
            return null;
        }

        if (TextoAlemDoPermitido(item) is { } excedente)
        {
            descartados.Add(new VinculoDescartado(
                identificador, excedente.Motivo, excedente.Campo));
            return null;
        }

        if (item.Curso!.Unidade is not { } unidade)
        {
            descartados.Add(new VinculoDescartado(
                identificador, MotivoDeDescarte.CursoSemUnidadeAcademica));
            return null;
        }

        if (item.AnoIngresso is not { } ano || item.PeriodoIngresso is not { } periodo)
        {
            descartados.Add(new VinculoDescartado(
                identificador, MotivoDeDescarte.SemPeriodoDeIngresso));
            return null;
        }

        return Montar(item, cpf, unidade, ano, periodo, descartados, identificador);
    }

    /// <summary>
    /// Um campo de texto e os dois tetos que ele precisa respeitar.
    /// </summary>
    /// <param name="NoContrato">
    /// Nulo quando o contrato não declara limite para o campo; então só o da réplica vale.
    /// </param>
    private readonly record struct TetoDeTexto(
        string Campo,
        Func<VinculoDiscentePayload, string?> Ler,
        int? NoContrato,
        int NaReplica);

    /// <summary>
    /// Os dois tetos de cada campo de texto, na ordem em que são conferidos.
    /// </summary>
    private static readonly TetoDeTexto[] TetosDeTexto =
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
    /// Devolve o primeiro campo de texto que estoura algum dos dois tetos, junto do motivo,
    /// ou nulo quando todos cabem.
    /// </summary>
    /// <remarks>
    /// Os dois tetos dizem coisas diferentes e não podem ser confundidos. Passar do que o
    /// contrato declara é a origem entregando além do que promete, e precisa aparecer como
    /// tal — as colunas são mais largas do que o contrato em quase todo campo, então um
    /// teto só pela coluna deixaria a origem divergir por uma faixa larga sem que nada
    /// apontasse a divergência. Passar do que a coluna comporta, aí sim, é o registro que a
    /// réplica não guarda; sem esta conferência ele só seria recusado na gravação,
    /// derrubando o lote inteiro e levando junto os vínculos válidos que o acompanhassem.
    /// </remarks>
    private static (string Campo, MotivoDeDescarte Motivo)? TextoAlemDoPermitido(
        VinculoDiscentePayload item)
    {
        foreach (TetoDeTexto teto in TetosDeTexto)
        {
            if (teto.Ler(item) is not { } valor)
            {
                continue;
            }

            if (teto.NoContrato is { } declarado && valor.Length > declarado)
            {
                return (teto.Campo, MotivoDeDescarte.ForaDoContrato);
            }

            if (valor.Length > teto.NaReplica)
            {
                return (teto.Campo, MotivoDeDescarte.NaoCabeNaReplica);
            }
        }

        return null;
    }

    /// <summary>
    /// Devolve o nome do primeiro campo obrigatório que a origem não entregou, ou nulo
    /// quando todos vieram.
    /// </summary>
    private static string? CampoObrigatorioAusente(VinculoDiscentePayload item)
    {
        if (item.IdDiscente <= 0)
        {
            return "idDiscente";
        }

        if (string.IsNullOrWhiteSpace(item.Matricula))
        {
            return "matricula";
        }

        // A origem promete matrícula composta só por dígitos. Letra ou pontuação aqui é
        // entrega fora do combinado, não valor que a réplica não comporta.
        if (!VinculoDiscenteSnapshot.ApenasDigitos(item.Matricula))
        {
            return "matricula";
        }

        if (string.IsNullOrWhiteSpace(item.Nome))
        {
            return "nome";
        }

        if (string.IsNullOrWhiteSpace(item.Nivel))
        {
            return "nivel";
        }

        if (string.IsNullOrWhiteSpace(item.Cpf))
        {
            return "cpf";
        }

        if (item.Curso is null)
        {
            return "curso";
        }

        if (item.Curso.Id <= 0)
        {
            return "curso.id";
        }

        if (string.IsNullOrWhiteSpace(item.Curso.Nome))
        {
            return "curso.nome";
        }

        if (item.Situacao is null)
        {
            return "situacao";
        }

        // O contrato declara a situação como conjunto fechado, então aqui não basta ser
        // positiva: um identificador que a origem não declara é um estado que ela própria
        // não trata como vínculo, e guardá-lo faria a réplica afirmar algo que a origem não
        // afirma — com o CPF cifrado junto e contado como sucesso.
        if (!SituacoesDoContrato.Declarada(item.Situacao.Id))
        {
            return "situacao.id";
        }

        return string.IsNullOrWhiteSpace(item.Situacao.Descricao) ? "situacao.descricao" : null;
    }

    private static VinculoDecodificado? Montar(
        VinculoDiscentePayload item,
        Cpf cpf,
        UnidadePayload unidade,
        int ano,
        int periodo,
        List<VinculoDescartado> descartados,
        long? identificador)
    {
        CursoSigaaSnapshot? curso = CursoSigaaSnapshot
            .Criar(item.Curso!.Id, item.Curso.Nome, item.Curso.CodigoEmec, unidade.Id, unidade.Nome)
            .Match<CursoSigaaSnapshot?>(c => c, _ => null);

        SituacaoAcademicaSnapshot? situacao = SituacaoAcademicaSnapshot
            .Criar(item.Situacao!.Id, item.Situacao.Descricao, item.Situacao.SituacaoVinculo)
            .Match<SituacaoAcademicaSnapshot?>(s => s, _ => null);

        PeriodoIngresso? ingresso = PeriodoIngresso
            .Criar(ano, periodo)
            .Match<PeriodoIngresso?>(p => p, _ => null);

        if (curso is null || situacao is null || ingresso is null)
        {
            descartados.Add(new VinculoDescartado(
                identificador, MotivoDeDescarte.ForaDoContrato, DescreverParteInvalida(curso, situacao)));
            return null;
        }

        VinculoDiscenteSnapshot? snapshot = VinculoDiscenteSnapshot
            .Criar(item.IdDiscente, item.Matricula, cpf, item.Nome, item.Nivel, curso, situacao, ingresso)
            .Match<VinculoDiscenteSnapshot?>(s => s, _ => null);

        if (snapshot is null)
        {
            descartados.Add(new VinculoDescartado(
                identificador, MotivoDeDescarte.ForaDoContrato, "vínculo"));
            return null;
        }

        return new VinculoDecodificado(VinculoDiscente.Criar(snapshot), ResumirConteudo(snapshot));
    }

    private static string DescreverParteInvalida(
        CursoSigaaSnapshot? curso,
        SituacaoAcademicaSnapshot? situacao)
    {
        if (curso is null)
        {
            return "curso";
        }

        return situacao is null ? "situacao" : "ingresso";
    }

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
    private static string ResumirConteudo(VinculoDiscenteSnapshot snapshot)
    {
        StringBuilder canonico = new();

        Acrescentar(canonico, snapshot.IdDiscenteSigaa.ToString(CultureInfo.InvariantCulture));
        Acrescentar(canonico, snapshot.Matricula);
        Acrescentar(canonico, snapshot.Nome);
        Acrescentar(canonico, snapshot.Nivel);
        Acrescentar(canonico, snapshot.Curso.Id.ToString(CultureInfo.InvariantCulture));
        Acrescentar(canonico, snapshot.Curso.Nome);
        Acrescentar(canonico, snapshot.Curso.CodigoEmec);
        Acrescentar(canonico, snapshot.Curso.UnidadeId.ToString(CultureInfo.InvariantCulture));
        Acrescentar(canonico, snapshot.Curso.UnidadeNome);
        Acrescentar(canonico, snapshot.Situacao.Id.ToString(CultureInfo.InvariantCulture));
        Acrescentar(canonico, snapshot.Situacao.Descricao);
        Acrescentar(canonico, snapshot.Situacao.Vinculo);
        Acrescentar(canonico, snapshot.Ingresso.Ano.ToString(CultureInfo.InvariantCulture));
        Acrescentar(canonico, snapshot.Ingresso.Periodo.ToString(CultureInfo.InvariantCulture));

        byte[] resumo = SHA256.HashData(Encoding.UTF8.GetBytes(canonico.ToString()));
        return Convert.ToHexStringLower(resumo);
    }

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
}
