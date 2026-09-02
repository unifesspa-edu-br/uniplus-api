namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Acl;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.Errors;
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
/// Separa duas situações que se parecem e exigem tratamento oposto. Um campo que o
/// contrato declara obrigatório vindo ausente é <b>quebra do contrato</b>: a origem não
/// está entregando o que prometeu, e insistir corromperia a réplica aos poucos — a
/// execução falha. Já um campo que o contrato permite deixar em branco e que o modelo da
/// réplica exige é <b>um registro que não serve</b>: a origem cumpriu sua parte, o vínculo
/// é descartado, contado, e a leitura segue para o próximo.
/// </para>
/// <para>
/// Confundir as duas quebra a sincronização de um jeito ou de outro: tratar a segunda como
/// a primeira faz a execução abortar todo dia por causa dos vínculos sem unidade
/// acadêmica; tratar a primeira como a segunda deixa uma mudança silenciosa de contrato
/// esvaziar a réplica sem que ninguém perceba.
/// </para>
/// </remarks>
public static class DecodificadorDeVinculos
{
    /// <summary>
    /// Traduz uma página inteira.
    /// </summary>
    /// <returns>
    /// Os vínculos aceitos e os descartados, ou falha quando a origem entrega algo fora do
    /// contrato — caso em que a página inteira é recusada.
    /// </returns>
    public static Result<ResultadoDaDecodificacao> Decodificar(IReadOnlyList<VinculoDiscentePayload> pagina)
    {
        ArgumentNullException.ThrowIfNull(pagina);

        List<VinculoDecodificado> aceitos = new(pagina.Count);
        List<VinculoDescartado> descartados = [];

        foreach (VinculoDiscentePayload item in pagina)
        {
            Result<VinculoDecodificado?> traduzido = Traduzir(item, descartados);

            if (traduzido.IsFailure)
            {
                return traduzido.Match(
                    _ => throw new InvalidOperationException("Resultado de falha sem erro."),
                    Result<ResultadoDaDecodificacao>.Failure);
            }

            if (traduzido.Value is { } vinculo)
            {
                aceitos.Add(vinculo);
            }
        }

        return Result<ResultadoDaDecodificacao>.Success(
            new ResultadoDaDecodificacao(aceitos, descartados));
    }

    /// <summary>
    /// Traduz um vínculo. Devolve nulo com sucesso quando o vínculo foi descartado — o
    /// descarte já ficou registrado em <paramref name="descartados"/>.
    /// </summary>
    private static Result<VinculoDecodificado?> Traduzir(
        VinculoDiscentePayload item,
        List<VinculoDescartado> descartados)
    {
        if (item is null)
        {
            return Falha(DiscentesErrorCodes.Payload.CampoObrigatorioAusente, "vínculo");
        }

        // Campos que o contrato declara obrigatórios. Ausência aqui é quebra do contrato.
        if (item.IdDiscente <= 0)
        {
            return Falha(DiscentesErrorCodes.Payload.CampoObrigatorioAusente, "idDiscente");
        }

        if (string.IsNullOrWhiteSpace(item.Matricula))
        {
            return Falha(DiscentesErrorCodes.Payload.CampoObrigatorioAusente, "matricula");
        }

        if (string.IsNullOrWhiteSpace(item.Nome))
        {
            return Falha(DiscentesErrorCodes.Payload.CampoObrigatorioAusente, "nome");
        }

        if (string.IsNullOrWhiteSpace(item.Nivel))
        {
            return Falha(DiscentesErrorCodes.Payload.CampoObrigatorioAusente, "nivel");
        }

        if (item.Curso is null)
        {
            return Falha(DiscentesErrorCodes.Payload.CampoObrigatorioAusente, "curso");
        }

        if (item.Situacao is null)
        {
            return Falha(DiscentesErrorCodes.Payload.CampoObrigatorioAusente, "situacao");
        }

        if (string.IsNullOrWhiteSpace(item.Cpf))
        {
            return Falha(DiscentesErrorCodes.Payload.CampoObrigatorioAusente, "cpf");
        }

        if (item.Curso.Id <= 0)
        {
            return Falha(DiscentesErrorCodes.Payload.CampoObrigatorioAusente, "curso.id");
        }

        if (string.IsNullOrWhiteSpace(item.Curso.Nome))
        {
            return Falha(DiscentesErrorCodes.Payload.CampoObrigatorioAusente, "curso.nome");
        }

        if (item.Situacao.Id <= 0)
        {
            return Falha(DiscentesErrorCodes.Payload.CampoObrigatorioAusente, "situacao.id");
        }

        if (string.IsNullOrWhiteSpace(item.Situacao.Descricao))
        {
            return Falha(DiscentesErrorCodes.Payload.CampoObrigatorioAusente, "situacao.descricao");
        }

        // O identificador da pessoa fora do formato acordado também é quebra de contrato, e
        // por isso é conferido aqui, junto das demais — e não depois dos descartes. Adiante,
        // o primeiro descarte encerraria a tradução deste vínculo e a conferência nunca
        // aconteceria: um CPF corrompido passaria por "registro que não serve" toda vez que
        // viesse acompanhado de um curso sem unidade, escondendo a quebra do contrato
        // justamente no caso mais frequente da origem.
        Result<Cpf> cpf = Cpf.Criar(item.Cpf);
        if (cpf.IsFailure)
        {
            return Result<VinculoDecodificado?>.Failure(new DomainError(
                DiscentesErrorCodes.Payload.CpfInvalido,
                "A origem entregou um CPF fora do formato acordado."));
        }

        // Daqui em diante, só o que o contrato permite em branco e a réplica exige.
        // Ausência descarta o vínculo, sem interromper a leitura.
        if (item.Curso.Unidade is not { } unidade)
        {
            descartados.Add(new VinculoDescartado(
                item.IdDiscente, MotivoDeDescarte.CursoSemUnidadeAcademica));
            return Result<VinculoDecodificado?>.Success(null);
        }

        if (item.AnoIngresso is not { } ano || item.PeriodoIngresso is not { } periodo)
        {
            descartados.Add(new VinculoDescartado(
                item.IdDiscente, MotivoDeDescarte.SemPeriodoDeIngresso));
            return Result<VinculoDecodificado?>.Success(null);
        }

        return cpf.Match(
            valido => Montar(item, valido, unidade, ano, periodo),
            Recusar);
    }

    private static Result<VinculoDecodificado?> Montar(
        VinculoDiscentePayload item,
        Cpf cpf,
        UnidadePayload unidade,
        int ano,
        int periodo) =>
        CursoSigaaSnapshot.Criar(
                item.Curso!.Id, item.Curso.Nome, item.Curso.CodigoEmec, unidade.Id, unidade.Nome)
            .Match(
                curso => SituacaoAcademicaSnapshot.Criar(
                        item.Situacao!.Id, item.Situacao.Descricao, item.Situacao.SituacaoVinculo)
                    .Match(
                        situacao => PeriodoIngresso.Criar(ano, periodo)
                            .Match(
                                ingresso => MontarSnapshot(item, cpf, curso, situacao, ingresso),
                                Recusar),
                        Recusar),
                Recusar);

    private static Result<VinculoDecodificado?> MontarSnapshot(
        VinculoDiscentePayload item,
        Cpf cpf,
        CursoSigaaSnapshot curso,
        SituacaoAcademicaSnapshot situacao,
        PeriodoIngresso ingresso) =>
        VinculoDiscenteSnapshot.Criar(
                item.IdDiscente, item.Matricula, cpf, item.Nome, item.Nivel, curso, situacao, ingresso)
            .Match(
                snapshot => Result<VinculoDecodificado?>.Success(new VinculoDecodificado(
                    VinculoDiscente.Criar(snapshot),
                    ResumirConteudo(snapshot))),
                Recusar);

    private static Result<VinculoDecodificado?> Recusar(DomainError erro) =>
        Result<VinculoDecodificado?>.Failure(erro);

    /// <summary>
    /// Resume, num valor curto, o que veio da origem para este vínculo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O resumo é calculado sobre os valores já traduzidos, em ordem fixa, e não sobre o
    /// texto recebido: assim ele não muda quando a origem reordena campos ou altera
    /// espaçamento, e muda sempre que qualquer campo coberto muda. Campo ausente entra
    /// como marca própria, para que ausência e vazio não se confundam.
    /// </para>
    /// <para>
    /// <b>O CPF fica de fora, deliberadamente.</b> O resumo é guardado ao lado dos demais
    /// campos, que ficam legíveis na mesma linha — quem tivesse a tabela conheceria tudo
    /// menos o CPF e poderia testar, um a um, os pouco mais de um bilhão de CPFs válidos
    /// até achar o que reproduz o resumo. Isso é rápido com equipamento comum e devolveria
    /// em claro justamente o dado que a cifra em repouso protege. Incluir o CPF aqui
    /// desfaria essa proteção.
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

    private static Result<VinculoDecodificado?> Falha(string codigo, string campo) =>
        Result<VinculoDecodificado?>.Failure(new DomainError(
            codigo,
            $"A origem entregou um vínculo sem o campo obrigatório '{campo}'."));
}
