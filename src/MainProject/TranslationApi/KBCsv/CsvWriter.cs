using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Data;

namespace TranslationApi.KBCsv
{
    /// <summary>
    /// Provides a mechanism via which CSV data can be easily written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>CsvWriter</c> class allows CSV data to be written to any stream-based destination. By default, CSV values are separated by commas
    /// (<c>,</c>) and delimited by double quotes (<c>"</c>). If necessary, custom characters can be specified when creating the <c>CsvWriter</c>.
    /// </para>
    /// <para>
    /// The number of records that have been written so far is exposed via the <see cref="RecordNumber"/> property. Writing a header record does not
    /// increment this property.
    /// </para>
    /// <para>
    /// A CSV header record can be optionally written by the <c>CsvWriter</c>. If a header record is to be written, it must be done first thing with
    /// the <see cref="WriteHeaderRecord"/> method. If a header record is written, it is exposed via the <see cref="HeaderRecord"/> property.
    /// </para>
    /// <para>
    /// Data records can be written with the <see cref="WriteDataRecord"/> or <see cref="WriteDataRecords"/> methods. These methods are overloaded to
    /// accept either instances of <see cref="DataRecord"/> or an array of <c>string</c>s.
    /// </para>
    /// </remarks>
    /// <threadsafety>
    /// The <c>CsvWriter</c> class does not lock internally. Therefore, it is unsafe to share an instance across threads without implementing your own
    /// synchronisation solution.
    /// </threadsafety>
    /// <example>
    /// <para>
    /// The following example writes some simple CSV data to a file:
    /// </para>
    /// <para>
    /// <code lang="C#">
    /// <![CDATA[
    /// using (CsvWriter writer = new CsvWriter(@"C:\Temp\data.csv")) {
    ///		writer.WriteHeaderRecord("Name", "Age", "Gender");
    ///		writer.WriteDataRecord("Kent", 25, Gender.Male);
    ///		writer.WriteDataRecord("Belinda", 26, Gender.Female);
    ///		writer.WriteDataRecord("Tempany", 0, Gender.Female);
    /// }
    /// ]]>
    /// </code>
    /// </para>
    /// <para>
    /// <code lang="vb">
    /// <![CDATA[
    /// Dim writer As CsvWriter = Nothing
    /// 
    /// Try
    ///		writer = New CsvWriter("C:\Temp\data.csv")
    ///		writer.WriteHeaderRecord("Name", "Age", "Gender")
    ///		writer.WriteDataRecord("Kent", 25, Gender.Male)
    ///		writer.WriteDataRecord("Belinda", 26, Gender.Female)
    ///		writer.WriteDataRecord("Tempany", 0, Gender.Female)
    /// Finally
    ///		If (Not writer Is Nothing) Then
    ///			writer.Close()
    ///		End If
    /// End Try
    /// ]]>
    /// </code>
    /// </para>
    /// </example>
    /// <example>
    /// <para>
    /// The following example writes the contents of a <c>DataTable</c> to a <see cref="MemoryStream"/>. CSV values are separated by tabs and
    /// delimited by the pipe characters (<c>|</c>). Linux-style line breaks are written by the <c>CsvWriter</c>, regardless of the hosting platform:
    /// </para>
    /// <para>
    /// <code lang="C#">
    /// <![CDATA[
    /// DataTable table = GetDataTable();
    /// MemoryStream memStream = new MemoryStream();
    /// 
    /// using (CsvWriter writer = new CsvWriter(memStream)) {
    ///		writer.NewLine = "\r";
    ///		writer.ValueSeparator = '\t';
    ///		writer.ValueDelimiter = '|';
    ///		writer.WriteAll(table, true);
    /// }
    /// ]]>
    /// </code>
    /// </para>
    /// <para>
    /// <code lang="vb">
    /// <![CDATA[
    /// Dim table As DataTable = GetDataTable
    /// Dim memStream As MemoryStream = New MemoryStream
    /// Dim writer As CsvWriter = Nothing
    /// 
    /// Try
    ///		writer = New CsvWriter(memStream)
    ///		writer.NewLine = vbLf
    ///		writer.ValueSeparator = vbTab
    ///		writer.ValueDelimiter = "|"c
    ///		writer.WriteAll(table, True)
    /// Finally
    ///		If (Not writer Is Nothing) Then
    ///			writer.Close()
    ///		End If
    /// End Try
    /// ]]>
    /// </code>
    /// </para>
    /// </example>
    public class CsvWriter : IDisposable
    {
        private static readonly Encoding defaultEncoding = Encoding.UTF8;

        /// <summary>
        /// The <see cref="TextWriter"/> used to output CSV data.
        /// </summary>
        private TextWriter _writer;

        /// <summary>
        /// Set to <see langword="true"/> when this <c>CsvWriter</c> is disposed.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// See <see cref="HeaderRecord"/>.
        /// </summary>
        private HeaderRecord _headerRecord;

        /// <summary>
        /// See <see cref="AlwaysDelimit"/>.
        /// </summary>
        private bool _alwaysDelimit;

        /// <summary>
        /// The buffer of characters containing the current value.
        /// </summary>
        private char[] _valueBuffer;

        /// <summary>
        /// The last valid index into <see cref="_valueBuffer"/>.
        /// </summary>
        private int _valueBufferEndIndex;

        /// <summary>
        /// See <see cref="ValueSeparator"/>.
        /// </summary>
        private char _valueSeparator;

        /// <summary>
        /// See <see cref="ValueDelimiter"/>.
        /// </summary>
        private char _valueDelimiter;

        /// <summary>
        /// See <see cref="RecordNumber"/>.
        /// </summary>
        private long _recordNumber;

        /// <summary>
        /// Set to <see langword="true"/> once the first record is written.
        /// </summary>
        private bool _passedFirstRecord;

        /// <summary>
        /// The space character.
        /// </summary>
        private const char SPACE = ' ';

        /// <summary>
        /// The carriage return character. Escape code is <c>\r</c>.
        /// </summary>
        private const char CR = (char)0x0d;

        /// <summary>
        /// The line-feed character. Escape code is <c>\n</c>.
        /// </summary>
        private const char LF = (char)0x0a;

        /// <summary>
        /// Gets the encoding of the underlying writer for this <c>CsvWriter</c>.
        /// </summary>
        public Encoding Encoding
        {
            get
            {
                EnsureNotDisposed();
                return _writer.Encoding;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether values should always be delimited.
        /// </summary>
        /// <remarks>
        /// By default the <c>CsvWriter</c> will only delimit values that require delimiting. Setting this property to <c>true</c> will ensure that all written values are
        /// delimited.
        /// </remarks>
        public bool AlwaysDelimit
        {
            get
            {
                EnsureNotDisposed();
                return _alwaysDelimit;
            }
            set
            {
                EnsureNotDisposed();
                _alwaysDelimit = value;
            }
        }

        /// <summary>
        /// Gets or sets the character placed between values in the CSV data.
        /// </summary>
        /// <remarks>
        /// This property retrieves the character that this <c>CsvWriter</c> will use to separate distinct values in the CSV data. The default value
        /// of this property is a comma (<c>,</c>).
        /// </remarks>
        public char ValueSeparator
        {
            get
            {
                EnsureNotDisposed();
                return _valueSeparator;
            }
            set
            {
                EnsureNotDisposed();

                if (value == _valueDelimiter)
                    throw new Exception("value-separator-same-as-value-delimiter");

                if (value == SPACE)
                    throw new Exception("value-separator-or-value-delimiter-space");

                _valueSeparator = value;
            }
        }

        /// <summary>
        /// Gets the character possibly placed around values in the CSV data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property retrieves the character that this <c>CsvWriter</c> will use to demarcate values in the CSV data. The default value of this
        /// property is a double quote (<c>"</c>).
        /// </para>
        /// <para>
        /// If <see cref="AlwaysDelimit"/> is <c>true</c>, then values written by this <c>CsvWriter</c> will always be delimited with this character. Otherwise, the
        /// <c>CsvWriter</c> will decide whether values must be delimited based on their content.
        /// </para>
        /// </remarks>
        public char ValueDelimiter
        {
            get
            {
                EnsureNotDisposed();
                return _valueDelimiter;
            }
            set
            {
                EnsureNotDisposed();

                if (value == _valueSeparator)
                    throw new Exception("value-separator-same-as-value-delimiter");

                if (value == SPACE)
                    throw new Exception("value-separator-or-value-delimiter-space");

                _valueDelimiter = value;
            }
        }

        /// <summary>
        /// Gets or sets the line terminator for this <c>CsvWriter</c>.
        /// </summary>
        /// <remarks>
        /// This property gets or sets the line terminator for the underlying <c>TextWriter</c> used by this <c>CsvWriter</c>. If this is set to <see langword="null"/> the
        /// default newline string is used instead.
        /// </remarks>
        public string NewLine
        {
            get
            {
                EnsureNotDisposed();
                return _writer.NewLine;
            }
            set
            {
                EnsureNotDisposed();
                _writer.NewLine = value;
            }
        }

        /// <summary>
        /// Gets the CSV header for this writer.
        /// </summary>
        /// <value>
        /// The CSV header record for this writer, or <see langword="null"/> if no header record applies.
        /// </value>
        /// <remarks>
        /// This property can be used to retrieve the <see cref="HeaderRecord"/> that represents the header record for this <c>CsvWriter</c>. If a
        /// header record has been written (using, for example, <see cref="WriteHeaderRecord"/>) then this property will retrieve the details of the
        /// header record. If a header record has not been written, this property will return <see langword="null"/>.
        /// </remarks>
        public HeaderRecord HeaderRecord
        {
            get
            {
                EnsureNotDisposed();
                return _headerRecord;
            }
        }

        /// <summary>
        /// Gets the current record number.
        /// </summary>
        /// <remarks>
        /// This property gives the number of records that the <c>CsvWriter</c> has written. The CSV header does not count. That is, calling
        /// <see cref="WriteHeaderRecord"/> will not increment this property. Only successful calls to <see cref="WriteDataRecord"/> (and related methods)
        /// will increment this property.
        /// </remarks>
        public long RecordNumber
        {
            get
            {
                EnsureNotDisposed();
                return _recordNumber;
            }
        }

        /// <summary>
        /// Constructs and initialises an instance of <c>CsvWriter</c> based on the information provided.
        /// </summary>
        /// <remarks>
        /// If the specified file already exists, it will be overwritten.
        /// </remarks>
        /// <param name="stream">
        /// The stream to which CSV data will be written.
        /// </param>
        public CsvWriter(Stream stream)
            : this(stream, defaultEncoding)
        {
        }

        /// <summary>
        /// Constructs and initialises an instance of <c>CsvWriter</c> based on the information provided.
        /// </summary>
        /// <remarks>
        /// If the specified file already exists, it will be overwritten.
        /// </remarks>
        /// <param name="stream">
        /// The stream to which CSV data will be written.
        /// </param>
        /// <param name="encoding">
        /// The encoding for the data in <paramref name="stream"/>.
        /// </param>
        public CsvWriter(Stream stream, Encoding encoding)
            : this(new StreamWriter(stream, encoding))
        {
        }

        /// <summary>
        /// Constructs and initialises an instance of <c>CsvWriter</c> based on the information provided.
        /// </summary>
        /// <remarks>
        /// If the specified file already exists, it will be overwritten.
        /// </remarks>
        /// <param name="path">
        /// The full path to the file to which CSV data will be written.
        /// </param>
        public CsvWriter(string path)
            : this(path, false, defaultEncoding)
        {
        }

        /// <summary>
        /// Constructs and initialises an instance of <c>CsvWriter</c> based on the information provided.
        /// </summary>
        /// <remarks>
        /// If the specified file already exists, it will be overwritten.
        /// </remarks>
        /// <param name="path">
        /// The full path to the file to which CSV data will be written.
        /// </param>
        /// <param name="encoding">
        /// The encoding for the data in <paramref name="path"/>.
        /// </param>
        public CsvWriter(string path, Encoding encoding)
            : this(path, false, encoding)
        {
        }

        /// <summary>
        /// Constructs and initialises an instance of <c>CsvWriter</c> based on the information provided.
        /// </summary>
        /// <remarks>
        /// If the specified file already exists, it will be overwritten.
        /// </remarks>
        /// <param name="path">
        /// The full path to the file to which CSV data will be written.
        /// </param>
        /// <param name="append">
        /// If <c>true</c>, data will be appended to the specified file.
        /// </param>
        public CsvWriter(string path, bool append)
            : this(path, append, defaultEncoding)
        {
        }

        /// <summary>
        /// Constructs and initialises an instance of <c>CsvWriter</c> based on the information provided.
        /// </summary>
        /// <remarks>
        /// If the specified file already exists, it will be overwritten.
        /// </remarks>
        /// <param name="path">
        /// The full path to the file to which CSV data will be written.
        /// </param>
        /// <param name="append">
        /// If <c>true</c>, data will be appended to the specified file.
        /// </param>
        /// <param name="encoding">
        /// The encoding for the data in <paramref name="path"/>.
        /// </param>
        public CsvWriter(string path, bool append, Encoding encoding)
            : this(new StreamWriter(path, append, encoding))
        {
        }

        /// <summary>
        /// Constructs and initialises an instance of <c>CsvWriter</c> based on the information provided.
        /// </summary>
        /// <param name="writer">
        /// The <see cref="TextWriter"/> to which CSV data will be written.
        /// </param>
        public CsvWriter(TextWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException("Writer");

            _writer = writer;
            _valueSeparator = CsvParser.DefaultValueSeparator;
            _valueDelimiter = CsvParser.DefaultValueDelimiter;
            _valueBuffer = new char[128];
        }

        /// <summary>
        /// Disposes of this <c>CsvWriter</c> instance.
        /// </summary>
        void IDisposable.Dispose()
        {
            Close();
            Dispose(true);
        }

        /// <summary>
        /// Allows sub classes to implement disposing logic.
        /// </summary>
        /// <param name="disposing">
        /// <see langword="true"/> if this method is being called in response to a <see cref="Dispose"/> call, or <see langword="false"/> if
        /// it is being called during finalization.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        /// Closes this <c>CsvWriter</c> instance and releases all resources acquired by it.
        /// </summary>
        /// <remarks>
        /// Once an instance of <c>CsvWriter</c> is no longer needed, call this method to immediately release any resources. Closing a <c>CsvWriter</c> is equivalent to
        /// disposing of it in a C# <c>using</c> block.
        /// </remarks>
        public void Close()
        {
            if (_writer != null)
            {
                _writer.Close();
            }

            _disposed = true;
        }

        /// <summary>
        /// Flushes the underlying buffer of this <c>CsvWriter</c>.
        /// </summary>
        /// <remarks>
        /// This method can be used to flush the underlying <c>Stream</c> that this <c>CsvWriter</c> writes to.
        /// </remarks>
        public void Flush()
        {
            EnsureNotDisposed();
            _writer.Flush();
        }

        /// <summary>
        /// Writes the specified header record.
        /// </summary>
        /// <remarks>
        /// This method writes the specified header record to the underlying <c>Stream</c>. Once successfully written, the header record is exposed via
        /// the <see cref="HeaderRecord"/> property.
        /// </remarks>
        /// <param name="headerRecord">
        /// The CSV header record to be written.
        /// </param>
        public void WriteHeaderRecord(HeaderRecord headerRecord)
        {
            if (headerRecord == null)
                throw new ArgumentNullException("HeaderRecord");
            
            if (_passedFirstRecord)
                throw new Exception("WriteHeaderRecord.passed-first-record");

            _headerRecord = headerRecord;
            WriteRecord(headerRecord.Values, false);
        }

        /// <summary>
        /// Writes a header record with the specified columns.
        /// </summary>
        /// <remarks>
        /// Each item in <paramref name="headerRecord"/> is converted to a <c>string</c> via its <c>ToString</c> implementation. If any item is <see langword="null"/>,
        /// it is substituted for an empty <c>string</c> (<see cref="string.Empty"/>).
        /// </remarks>
        /// <param name="headerRecord">
        /// An array of header column names.
        /// </param>
        public void WriteHeaderRecord(params object[] headerRecord)
        {
            if (headerRecord == null)
                throw new ArgumentNullException("HeaderRecord");

            string[] headerRecordAsStrings = new string[headerRecord.Length];

            for (int i = 0; i < headerRecordAsStrings.Length; ++i)
            {
                object o = headerRecord[i];

                if (o != null)
                {
                    headerRecordAsStrings[i] = o.ToString();
                }
                else
                {
                    headerRecordAsStrings[i] = string.Empty;
                }
            }

            WriteHeaderRecord(headerRecordAsStrings);
        }

        /// <summary>
        /// Writes the specified header record.
        /// </summary>
        /// <remarks>
        /// This method writes the specified header record to the underlying <c>Stream</c>. Once successfully written, the header record is exposed via
        /// the <see cref="HeaderRecord"/> property.
        /// </remarks>
        /// <param name="headerRecord">
        /// The CSV header record to be written.
        /// </param>
        public void WriteHeaderRecord(string[] headerRecord)
        {
            EnsureNotDisposed();

            if (headerRecord == null)
                throw new ArgumentNullException("HeaderRecord");

            if (_passedFirstRecord)
                throw new Exception("WriteHeaderRecord.passed-first-record");

            _headerRecord = new HeaderRecord(headerRecord, true);
            WriteRecord(headerRecord, false);
        }

        /// <summary>
        /// Writes the specified record to this <c>CsvWriter</c>.
        /// </summary>
        /// <remarks>
        /// This method writes a single data record to this <c>CsvWriter</c>. The <see cref="RecordNumber"/> property is incremented upon successfully writing
        /// the record.
        /// </remarks>
        /// <param name="dataRecord">
        /// The record to be written.
        /// </param>
        public void WriteDataRecord(DataRecord dataRecord)
        {
            EnsureNotDisposed();

            if (dataRecord == null)
                throw new ArgumentNullException("DataRecord");

            WriteRecord(dataRecord.Values, true);
        }


        /// <summary>
        /// Writes a data record with the specified values.
        /// </summary>
        /// <remarks>
        /// Each item in <paramref name="dataRecord"/> is converted to a <c>string</c> via its <c>ToString</c> implementation. If any item is <see langword="null"/>, it is substituted
        /// for an empty <c>string</c> (<see cref="string.Empty"/>).
        /// </remarks>
        /// <param name="dataRecord">
        /// An array of data values.
        /// </param>
        public void WriteDataRecord(params object[] dataRecord)
        {
            if (dataRecord == null)
                throw new ArgumentNullException("DataRecord");

            string[] dataRecordAsStrings = new string[dataRecord.Length];

            for (int i = 0; i < dataRecordAsStrings.Length; ++i)
            {
                object o = dataRecord[i];

                if (o != null)
                {
                    dataRecordAsStrings[i] = o.ToString();
                }
                else
                {
                    dataRecordAsStrings[i] = string.Empty;
                }
            }

            WriteDataRecord(dataRecordAsStrings);
        }

        /// <summary>
        /// Writes the specified record to this <c>CsvWriter</c>.
        /// </summary>
        /// <remarks>
        /// This method writes a single data record to this <c>CsvWriter</c>. The <see cref="RecordNumber"/> property is incremented upon successfully writing
        /// the record.
        /// </remarks>
        /// <param name="dataRecord">
        /// The record to be written.
        /// </param>
        public void WriteDataRecord(string[] dataRecord)
        {
            EnsureNotDisposed();

            if (dataRecord == null)
                throw new ArgumentNullException("DataRecord");

            WriteRecord(dataRecord, true);
        }

        /// <summary>
        /// Writes all records specified by <paramref name="dataRecords"/> to this <c>CsvWriter</c>.
        /// </summary>
        /// <remarks>
        /// This method writes all data records in <paramref name="dataRecords"/> to this <c>CsvWriter</c> and increments the <see cref="RecordNumber"/> property
        /// as records are written.
        /// </remarks>
        /// <param name="dataRecords">
        /// The records to be written.
        /// </param>
        public void WriteDataRecords(ICollection<DataRecord> dataRecords)
        {
            EnsureNotDisposed();

            if (dataRecords == null)
                throw new ArgumentNullException("DataRecords");

            foreach (DataRecord dataRecord in dataRecords)
            {
                WriteRecord(dataRecord.Values, true);
            }
        }

        /// <summary>
        /// Writes all records specified by <paramref name="dataRecords"/> to this <c>CsvWriter</c>.
        /// </summary>
        /// <remarks>
        /// This method writes all data records in <paramref name="dataRecords"/> to this <c>CsvWriter</c> and increments the <see cref="RecordNumber"/> property
        /// as records are written.
        /// </remarks>
        /// <param name="dataRecords">
        /// The records to be written.
        /// </param>
        public void WriteDataRecords(ICollection<string[]> dataRecords)
        {
            EnsureNotDisposed();

            if (dataRecords == null)
                throw new ArgumentNullException("DataRecords");

            foreach (string[] dataRecord in dataRecords)
            {
                WriteRecord(dataRecord, true);
            }
        }

        /// <summary>
        /// Writes the data in <paramref name="table"/> as CSV data.
        /// </summary>
        /// <remarks>
        /// This method writes all the data in <paramref name="table"/> to this <c>CsvWriter</c>, including a header record. If a header record has already
        /// been written to this <c>CsvWriter</c> this method will throw an exception. That being the case, you should use <see cref="WriteAll(DataTable, bool)"/>
        /// instead, specifying <see langword="false"/> for the second parameter.
        /// </remarks>
        /// <param name="table">
        /// The <c>DataTable</c> whose data is to be written as CSV data.
        /// </param>
        public void WriteAll(DataTable table)
        {
            WriteAll(table, true);
        }

        /// <summary>
        /// Writes the data in <paramref name="table"/> as CSV data.
        /// </summary>
        /// <remarks>
        /// This method writes all the data in <paramref name="table"/> to this <c>CsvWriter</c>, optionally writing a header record based on the columns in the
        /// table.
        /// </remarks>
        /// <param name="table">
        /// The <c>DataTable</c> whose data is to be written as CSV data.
        /// </param>
        /// <param name="writeHeaderRecord">
        /// If <see langword="true"/>, a CSV header will be written based on the column names for the table.
        /// </param>
        public void WriteAll(DataTable table, bool writeHeaderRecord)
        {
            EnsureNotDisposed();

            if (table == null)
                throw new ArgumentNullException("DataTable");

            if (writeHeaderRecord)
            {
                HeaderRecord headerRecord = new HeaderRecord();

                foreach (DataColumn column in table.Columns)
                {
                    headerRecord.Values.Add(column.ColumnName);
                }

                WriteHeaderRecord(headerRecord);
            }

            foreach (DataRow row in table.Rows)
            {
                DataRecord dataRecord = new DataRecord(_headerRecord);

                foreach (object item in row.ItemArray)
                {
                    dataRecord.Values.Add((item == null) ? string.Empty : item.ToString());
                }

                WriteDataRecord(dataRecord);
            }
        }

        /// <summary>
        /// Writes the first <see cref="DataTable"/> in <paramref name="dataSet"/> as CSV data.
        /// </summary>
        /// <remarks>
        /// This method writes all the data in the first table of <paramref name="dataSet"/> to this <c>CsvWriter</c>, including a header record.
        /// If a header record has already been written to this <c>CsvWriter</c> this method will throw an exception. That being the case, you
        /// should use <see cref="WriteAll(DataSet, bool)"/> instead, specifying <see langword="false"/> for the second parameter.
        /// </remarks>
        /// <param name="dataSet">
        /// The <c>DataSet</c> whose first table is to be written as CSV data.
        /// </param>
        public void WriteAll(DataSet dataSet)
        {
            WriteAll(dataSet, true);
        }

        /// <summary>
        /// Writes the first <see cref="DataTable"/> in <paramref name="dataSet"/> as CSV data.
        /// </summary>
        /// <remarks>
        /// This method writes all the data in the first table of <paramref name="dataSet"/> to this <c>CsvWriter</c>, optionally writing a header
        /// record based on the columns in the table.
        /// </remarks>
        /// <param name="dataSet">
        /// The <c>DataSet</c> whose first table is to be written as CSV data.
        /// </param>
        /// <param name="writeHeaderRecord">
        /// If <see langword="true"/>, a CSV header will be written based on the column names for the table.
        /// </param>
        public void WriteAll(DataSet dataSet, bool writeHeaderRecord)
        {
            EnsureNotDisposed();

            if (dataSet == null)
                throw new ArgumentNullException("DataSet");

            if (dataSet.Tables.Count == 0)
                throw new Exception("WriteAll.dataSet-no-table");

            WriteAll(dataSet.Tables[0], writeHeaderRecord);
        }

        /// <summary>
        /// Writes the specified record to the target <see cref="TextWriter"/>, ensuring all values are correctly separated and escaped.
        /// </summary>
        /// <remarks>
        /// This method is used internally by the <c>CsvWriter</c> to write CSV records.
        /// </remarks>
        /// <param name="record">
        /// The record to be written.
        /// </param>
        /// <param name="incrementRecordNumber">
        /// <see langword="true"/> if the record number should be incremented, otherwise <see langword="false"/>.
        /// </param>
        private void WriteRecord(IEnumerable<string> record, bool incrementRecordNumber)
        {
            bool firstValue = true;

            foreach (string value in record)
            {
                if (!firstValue)
                {
                    _writer.Write(_valueSeparator);
                }
                else
                {
                    firstValue = false;
                }

                WriteValue(value);
            }

            //uses the underlying TextWriter.NewLine property
            _writer.WriteLine();
            _passedFirstRecord = true;

            if (incrementRecordNumber)
            {
                ++_recordNumber;
            }
        }

        /// <summary>
        /// Writes the specified value to the target <see cref="TextWriter"/>, ensuring it is correctly escaped.
        /// </summary>
        /// <remarks>
        /// This method is used internally by the <c>CsvWriter</c> to write individual CSV values.
        /// </remarks>
        /// <param name="val">
        /// The value to be written.
        /// </param>
        private void WriteValue(string val)
        {
            _valueBufferEndIndex = 0;
            bool delimit = _alwaysDelimit;

            if (!string.IsNullOrEmpty(val))
            {
                //delimit to preserve white-space at the beginning or end of the value
                if ((val[0] == SPACE) || (val[val.Length - 1] == SPACE))
                {
                    delimit = true;
                }

                for (int i = 0; i < val.Length; ++i)
                {
                    char c = val[i];

                    if ((c == _valueSeparator) || (c == CR) || (c == LF))
                    {
                        //all these characters require the value to be delimited
                        AppendToValue(c);
                        delimit = true;
                    }
                    else if (c == _valueDelimiter)
                    {
                        //escape the delimiter by writing it twice
                        AppendToValue(_valueDelimiter);
                        AppendToValue(_valueDelimiter);
                        delimit = true;
                    }
                    else
                    {
                        AppendToValue(c);
                    }
                }
            }

            if (delimit)
            {
                _writer.Write(_valueDelimiter);
            }

            //write the value
            _writer.Write(_valueBuffer, 0, _valueBufferEndIndex);

            if (delimit)
            {
                _writer.Write(_valueDelimiter);
            }

            _valueBufferEndIndex = 0;
        }

        /// <summary>
        /// Appends the specified character onto the end of the current value.
        /// </summary>
        /// <param name="c">
        /// The character to append.
        /// </param>
        private void AppendToValue(char c)
        {
            EnsureValueBufferCapacity(1);
            _valueBuffer[_valueBufferEndIndex++] = c;
        }

        /// <summary>
        /// Ensures the value buffer contains enough space for <paramref name="count"/> more characters.
        /// </summary>
        private void EnsureValueBufferCapacity(int count)
        {
            if ((_valueBufferEndIndex + count) > _valueBuffer.Length)
            {
                char[] newBuffer = new char[Math.Max(_valueBuffer.Length * 2, (count >> 1) << 2)];

                //profiling revealed a loop to be faster than Array.Copy, despite Array.Copy having an internal implementation
                for (int i = 0; i < _valueBufferEndIndex; ++i)
                {
                    newBuffer[i] = _valueBuffer[i];
                }

                _valueBuffer = newBuffer;
            }
        }

        /// <summary>
        /// Makes sure the object isn't disposed and, if so, throws an exception.
        /// </summary>
        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new Exception("disposed");
        }
    }
}